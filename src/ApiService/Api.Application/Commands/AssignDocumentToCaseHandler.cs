using Api.Domain.Aggregates;
using Api.Domain.Ports;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Api.Application.Commands;

public sealed class AssignDocumentToCaseHandler
{
    private readonly IDocumentRepository _documentRepository;
    private readonly ICaseRepository _caseRepository;
    private readonly ILogger<AssignDocumentToCaseHandler> _logger;

    public AssignDocumentToCaseHandler(
        IDocumentRepository documentRepository,
        ICaseRepository caseRepository,
        ILogger<AssignDocumentToCaseHandler> logger)
    {
        _documentRepository = documentRepository;
        _caseRepository = caseRepository;
        _logger = logger;
    }

    public async Task<Result<Unit>> HandleAsync(
        DocumentId documentId, TenantId tenantId, CancellationToken ct = default)
    {
        var docResult = await _documentRepository.FindByIdUnfilteredAsync(documentId, ct);
        if (docResult.IsFailure)
            return Result<Unit>.Failure(docResult.Error);

        var document = docResult.Value;
        if (document is null)
            return Result<Unit>.Failure(new Error("NOT_FOUND", $"Document {documentId.Value} not found."));

        if (document.TenantId != tenantId)
            return Result<Unit>.Failure(new Error("FORBIDDEN",
                $"Document {documentId.Value} does not belong to tenant {tenantId.Value}."));

        // Find the first extracted field whose name contains "Name" (case-insensitive).
        var nameField = document.ExtractedFields
            .FirstOrDefault(f => f.Name.Contains("Name", StringComparison.OrdinalIgnoreCase));

        if (nameField is null)
        {
            _logger.LogInformation(
                "No name field found on document {DocumentId}. Skipping case assignment.",
                documentId.Value);
            return Result<Unit>.Success(Unit.Value);
        }

        var subjectName = (nameField.CorrectedValue ?? nameField.Value).Trim();
        if (string.IsNullOrWhiteSpace(subjectName))
        {
            _logger.LogInformation(
                "Name field is blank on document {DocumentId}. Skipping case assignment.",
                documentId.Value);
            return Result<Unit>.Success(Unit.Value);
        }

        // Find or create a case with this subject name.
        // Uses retry to handle race condition: if two messages try to create the same case
        // concurrently, the unique index on (tenant_id, subject_name) rejects the second insert,
        // and we retry by finding the case the other consumer created.
        var @case = await FindOrCreateCaseAsync(document, subjectName, tenantId, ct);
        if (@case is null)
            return Result<Unit>.Failure(new Error("CASE_ASSIGNMENT_FAILED",
                $"Failed to find or create case for subject '{subjectName}'."));

        _logger.LogInformation(
            "Document {DocumentId} assigned to case {CaseId} (subject: {SubjectName}).",
            documentId.Value, @case.Id.Value, subjectName);

        return Result<Unit>.Success(Unit.Value);
    }

    private async Task<Case?> FindOrCreateCaseAsync(
        IntakeDocument document, string subjectName, TenantId tenantId, CancellationToken ct)
    {
        var findResult = await _caseRepository.FindBySubjectNameAsync(subjectName, tenantId, ct);
        if (findResult.IsFailure)
            return null;

        if (findResult.Value is not null)
        {
            var existing = findResult.Value;
            existing.LinkDocument(document);
            document.AssignToCase(existing.Id);
            var updateResult = await _caseRepository.UpdateAsync(existing, ct);
            return updateResult.IsFailure ? null : existing;
        }

        var @case = Case.Create(tenantId, subjectName);
        @case.LinkDocument(document);
        document.AssignToCase(@case.Id);

        var saveResult = await _caseRepository.SaveAsync(@case, ct);
        if (saveResult.IsSuccess)
            return @case;

        // Unique constraint violation: another consumer created this case concurrently.
        // Retry by finding the case they created.
        _logger.LogInformation(
            "Case save conflict for subject '{SubjectName}'. Retrying find.", subjectName);

        var retryFind = await _caseRepository.FindBySubjectNameAsync(subjectName, tenantId, ct);
        if (retryFind.IsFailure || retryFind.Value is null)
            return null;

        var found = retryFind.Value;
        found.LinkDocument(document);
        document.AssignToCase(found.Id);
        var retryUpdate = await _caseRepository.UpdateAsync(found, ct);
        return retryUpdate.IsFailure ? null : found;
    }
}
