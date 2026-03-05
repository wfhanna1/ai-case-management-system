using Api.Application.DTOs;
using Api.Domain.Aggregates;

namespace Api.Application.Mappings;

public static class FormTemplateMappings
{
    public static FormTemplateDto ToDto(FormTemplate template) =>
        new(template.Id.Value,
            template.TenantId.Value,
            template.Name,
            template.Description,
            template.Type.ToString(),
            template.IsActive,
            template.CreatedAt,
            template.UpdatedAt,
            template.Fields.Select(f => new TemplateFieldDto(
                f.Label, f.FieldType.ToString(), f.IsRequired, f.Options)).ToList());
}
