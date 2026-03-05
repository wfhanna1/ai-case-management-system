using SharedKernel;

namespace Api.Domain.Aggregates;

public sealed class FormTemplate : AggregateRoot<FormTemplateId>
{
    public TenantId TenantId { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public TemplateType Type { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    private readonly List<TemplateField> _fields = [];
    public IReadOnlyList<TemplateField> Fields => _fields.AsReadOnly();

    // Required by EF Core for materialization from database.
    private FormTemplate() : base(FormTemplateId.New())
    {
        TenantId = null!;
        Name = null!;
        Description = null!;
    }

    private FormTemplate(
        FormTemplateId id,
        TenantId tenantId,
        string name,
        string description,
        TemplateType type,
        List<TemplateField> fields) : base(id)
    {
        TenantId = tenantId;
        Name = name;
        Description = description;
        Type = type;
        IsActive = true;
        CreatedAt = DateTimeOffset.UtcNow;
        _fields = [..fields];
    }

    public static FormTemplate Create(
        TenantId tenantId,
        string name,
        string description,
        TemplateType type,
        List<TemplateField> fields)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new FormTemplate(FormTemplateId.New(), tenantId, name, description, type, fields);
    }

    public void Update(string name, string description, List<TemplateField> fields)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
        Description = description;
        _fields.Clear();
        _fields.AddRange(fields);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
