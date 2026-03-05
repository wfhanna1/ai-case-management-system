using Api.Domain.Aggregates;
using SharedKernel;

namespace Api.Domain.Tests;

public sealed class FormTemplateTests
{
    private readonly TenantId _tenantId = new(Guid.NewGuid());

    [Fact]
    public void Create_returns_template_with_correct_properties()
    {
        var fields = new List<TemplateField>
        {
            new("FullName", FieldType.Text, true, null),
            new("DateOfBirth", FieldType.Date, true, null)
        };

        var template = FormTemplate.Create(
            _tenantId,
            "Child Welfare Intake",
            "Template for child welfare cases",
            TemplateType.ChildWelfare,
            fields);

        Assert.Equal("Child Welfare Intake", template.Name);
        Assert.Equal("Template for child welfare cases", template.Description);
        Assert.Equal(TemplateType.ChildWelfare, template.Type);
        Assert.Equal(_tenantId, template.TenantId);
        Assert.Equal(2, template.Fields.Count);
        Assert.True(template.IsActive);
    }

    [Fact]
    public void Create_rejects_empty_name()
    {
        Assert.Throws<ArgumentException>(() =>
            FormTemplate.Create(_tenantId, "", "desc", TemplateType.ChildWelfare, []));
    }

    [Fact]
    public void Create_rejects_null_name()
    {
        Assert.Throws<ArgumentNullException>(() =>
            FormTemplate.Create(_tenantId, null!, "desc", TemplateType.ChildWelfare, []));
    }

    [Fact]
    public void Create_allows_empty_fields_list()
    {
        var template = FormTemplate.Create(
            _tenantId, "Empty Template", "No fields yet", TemplateType.HousingAssistance, []);

        Assert.Empty(template.Fields);
    }

    [Fact]
    public void Update_changes_name_and_description()
    {
        var template = FormTemplate.Create(
            _tenantId, "Original", "Original desc", TemplateType.ChildWelfare, []);

        template.Update("Updated", "Updated desc", [new("Field1", FieldType.Text, false, null)]);

        Assert.Equal("Updated", template.Name);
        Assert.Equal("Updated desc", template.Description);
        Assert.Single(template.Fields);
    }

    [Fact]
    public void Update_rejects_empty_name()
    {
        var template = FormTemplate.Create(
            _tenantId, "Original", "desc", TemplateType.ChildWelfare, []);

        Assert.Throws<ArgumentException>(() =>
            template.Update("", "desc", []));
    }

    [Fact]
    public void Deactivate_sets_is_active_false()
    {
        var template = FormTemplate.Create(
            _tenantId, "Template", "desc", TemplateType.ChildWelfare, []);

        template.Deactivate();

        Assert.False(template.IsActive);
    }

    [Fact]
    public void Activate_sets_is_active_true()
    {
        var template = FormTemplate.Create(
            _tenantId, "Template", "desc", TemplateType.ChildWelfare, []);
        template.Deactivate();

        template.Activate();

        Assert.True(template.IsActive);
    }

    [Fact]
    public void Fields_are_stored_as_value_objects()
    {
        var field1 = new TemplateField("Name", FieldType.Text, true, null);
        var field2 = new TemplateField("Name", FieldType.Text, true, null);

        Assert.Equal(field1, field2);
    }

    [Fact]
    public void Different_fields_are_not_equal()
    {
        var field1 = new TemplateField("Name", FieldType.Text, true, null);
        var field2 = new TemplateField("Email", FieldType.Text, true, null);

        Assert.NotEqual(field1, field2);
    }

    [Fact]
    public void TemplateField_with_options_stores_them()
    {
        var field = new TemplateField("Gender", FieldType.Select, true, "[\"Male\",\"Female\",\"Other\"]");

        Assert.Equal("Gender", field.Label);
        Assert.Equal(FieldType.Select, field.FieldType);
        Assert.True(field.IsRequired);
        Assert.Equal("[\"Male\",\"Female\",\"Other\"]", field.Options);
    }
}
