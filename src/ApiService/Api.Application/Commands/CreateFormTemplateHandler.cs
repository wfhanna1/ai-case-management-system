using Api.Application.DTOs;
using Api.Application.Mappings;
using Api.Domain.Aggregates;
using Api.Domain.Ports;
using SharedKernel;

namespace Api.Application.Commands;

public sealed class CreateFormTemplateHandler
{
    private readonly IFormTemplateRepository _repository;

    public CreateFormTemplateHandler(IFormTemplateRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<FormTemplateDto>> HandleAsync(
        Guid tenantId,
        CreateFormTemplateRequest request,
        CancellationToken ct = default)
    {
        if (!Enum.TryParse<TemplateType>(request.Type, out var templateType))
            return Result<FormTemplateDto>.Failure(
                new Error("INVALID_TEMPLATE_TYPE", $"'{request.Type}' is not a valid template type."));

        var fields = new List<TemplateField>();
        foreach (var f in request.Fields)
        {
            if (!Enum.TryParse<FieldType>(f.FieldType, out var fieldType))
                return Result<FormTemplateDto>.Failure(
                    new Error("INVALID_FIELD_TYPE", $"'{f.FieldType}' is not a valid field type."));

            fields.Add(new TemplateField(f.Label, fieldType, f.IsRequired, f.Options));
        }

        var template = FormTemplate.Create(
            new TenantId(tenantId),
            request.Name,
            request.Description,
            templateType,
            fields);

        var saveResult = await _repository.SaveAsync(template, ct);
        if (saveResult.IsFailure)
            return Result<FormTemplateDto>.Failure(saveResult.Error);

        return Result<FormTemplateDto>.Success(FormTemplateMappings.ToDto(template));
    }
}
