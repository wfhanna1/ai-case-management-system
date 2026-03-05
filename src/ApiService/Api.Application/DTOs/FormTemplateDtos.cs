namespace Api.Application.DTOs;

public sealed record FormTemplateDto(
    Guid Id,
    Guid TenantId,
    string Name,
    string Description,
    string Type,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<TemplateFieldDto> Fields);

public sealed record TemplateFieldDto(
    string Label,
    string FieldType,
    bool IsRequired,
    string? Options);

public sealed record CreateFormTemplateRequest(
    string Name,
    string Description,
    string Type,
    List<TemplateFieldDto> Fields);
