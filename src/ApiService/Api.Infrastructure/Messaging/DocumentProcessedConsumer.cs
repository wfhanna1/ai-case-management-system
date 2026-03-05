using Api.Domain.Aggregates;
using Api.Infrastructure.Persistence;
using MassTransit;
using Messaging.Contracts.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Api.Infrastructure.Messaging;

/// <summary>
/// Consumes DocumentProcessedEvent from the OCR worker and updates the
/// corresponding IntakeDocument status to Completed.
/// Uses IgnoreQueryFilters because background consumers have no tenant context.
/// </summary>
public sealed class DocumentProcessedConsumer : IConsumer<DocumentProcessedEvent>
{
    private readonly IntakeDbContext _dbContext;
    private readonly ILogger<DocumentProcessedConsumer> _logger;

    public DocumentProcessedConsumer(
        IntakeDbContext dbContext,
        ILogger<DocumentProcessedConsumer> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<DocumentProcessedEvent> context)
    {
        var message = context.Message;

        _logger.LogInformation(
            "Received DocumentProcessedEvent. DocumentId={DocumentId} TenantId={TenantId} FieldCount={FieldCount}",
            message.DocumentId,
            message.TenantId,
            message.ExtractedFields.Count);

        var document = await _dbContext.Documents
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                d => d.Id == new DocumentId(message.DocumentId),
                context.CancellationToken);

        if (document is null)
        {
            _logger.LogWarning(
                "Document not found for DocumentId={DocumentId}. Skipping.",
                message.DocumentId);
            return;
        }

        if (document.TenantId != new TenantId(message.TenantId))
        {
            _logger.LogWarning(
                "Tenant mismatch for DocumentId={DocumentId}. Expected={Expected} Received={Received}. Skipping.",
                message.DocumentId,
                document.TenantId.Value,
                message.TenantId);
            return;
        }

        // Transition: Submitted -> Processing -> Completed.
        // MarkProcessing may fail if already Processing (idempotent retry); that is acceptable.
        var processingResult = document.MarkProcessing();
        if (processingResult.IsFailure && document.Status != DocumentStatus.Processing)
        {
            _logger.LogError(
                "Could not mark document as Processing. DocumentId={DocumentId} Status={Status} Error={Error}",
                message.DocumentId,
                document.Status,
                processingResult.Error.Message);
            return;
        }

        var completedResult = document.MarkCompleted();
        if (completedResult.IsFailure)
        {
            _logger.LogError(
                "Could not mark document as Completed. DocumentId={DocumentId} Error={Error}",
                message.DocumentId,
                completedResult.Error.Message);
            return;
        }

        await _dbContext.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation(
            "Document marked as Completed. DocumentId={DocumentId}",
            message.DocumentId);
    }
}
