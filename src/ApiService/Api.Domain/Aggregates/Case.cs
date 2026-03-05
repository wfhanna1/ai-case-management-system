using SharedKernel;

namespace Api.Domain.Aggregates;

public sealed class Case : AggregateRoot<CaseId>
{
    public TenantId TenantId { get; private set; }
    public string SubjectName { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<IntakeDocument> _documents = [];
    public IReadOnlyList<IntakeDocument> Documents => _documents.AsReadOnly();

    // Required by EF Core for materialization from database.
    private Case() : base(CaseId.New())
    {
        TenantId = null!;
        SubjectName = null!;
    }

    private Case(CaseId id, TenantId tenantId, string subjectName) : base(id)
    {
        TenantId = tenantId;
        SubjectName = subjectName;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public static Case Create(TenantId tenantId, string subjectName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectName);
        return new Case(CaseId.New(), tenantId, subjectName);
    }

    public void LinkDocument(IntakeDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (!_documents.Any(d => d.Id == document.Id))
        {
            _documents.Add(document);
            UpdatedAt = DateTimeOffset.UtcNow;
        }
    }
}
