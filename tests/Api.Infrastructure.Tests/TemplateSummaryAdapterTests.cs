using Api.Infrastructure.Summary;

namespace Api.Infrastructure.Tests;

public sealed class TemplateSummaryAdapterTests
{
    private readonly TemplateSummaryAdapter _adapter = new();

    [Fact]
    public async Task GenerateSummaryAsync_EmptyFields_ReturnsNoFieldsAvailable()
    {
        var result = await _adapter.GenerateSummaryAsync(new Dictionary<string, string>());

        Assert.True(result.IsSuccess);
        Assert.Equal("No fields available.", result.Value);
    }

    [Fact]
    public async Task GenerateSummaryAsync_NameField_ProducesSubjectPrefix()
    {
        var fields = new Dictionary<string, string> { { "ClientName", "Jane Doe" } };

        var result = await _adapter.GenerateSummaryAsync(fields);

        Assert.True(result.IsSuccess);
        Assert.Contains("Subject: Jane Doe", result.Value);
    }

    [Fact]
    public async Task GenerateSummaryAsync_NameField_IsCaseInsensitive()
    {
        var fields = new Dictionary<string, string> { { "full_name", "John Smith" } };

        var result = await _adapter.GenerateSummaryAsync(fields);

        Assert.True(result.IsSuccess);
        Assert.Contains("Subject: John Smith", result.Value);
    }

    [Fact]
    public async Task GenerateSummaryAsync_TypeField_ProducesCategoryPrefix()
    {
        var fields = new Dictionary<string, string> { { "CaseType", "Child Welfare" } };

        var result = await _adapter.GenerateSummaryAsync(fields);

        Assert.True(result.IsSuccess);
        Assert.Contains("Category: Child Welfare", result.Value);
    }

    [Fact]
    public async Task GenerateSummaryAsync_CategoryField_ProducesCategoryPrefix()
    {
        var fields = new Dictionary<string, string> { { "Category", "Housing" } };

        var result = await _adapter.GenerateSummaryAsync(fields);

        Assert.True(result.IsSuccess);
        Assert.Contains("Category: Housing", result.Value);
    }

    [Fact]
    public async Task GenerateSummaryAsync_SafetyYes_AppendsSafetyFlag()
    {
        var fields = new Dictionary<string, string>
        {
            { "Name", "Test" },
            { "SafetyConcern", "Yes" }
        };

        var result = await _adapter.GenerateSummaryAsync(fields);

        Assert.True(result.IsSuccess);
        Assert.Contains("Safety concern flagged", result.Value);
    }

    [Fact]
    public async Task GenerateSummaryAsync_SafetyYes_CaseInsensitive()
    {
        var fields = new Dictionary<string, string>
        {
            { "Name", "Test" },
            { "Safety", "yes" }
        };

        var result = await _adapter.GenerateSummaryAsync(fields);

        Assert.True(result.IsSuccess);
        Assert.Contains("Safety concern flagged", result.Value);
    }

    [Fact]
    public async Task GenerateSummaryAsync_SafetyNo_DoesNotAppendFlag()
    {
        var fields = new Dictionary<string, string>
        {
            { "Name", "Test" },
            { "SafetyConcern", "No" }
        };

        var result = await _adapter.GenerateSummaryAsync(fields);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain("Safety concern flagged", result.Value);
    }

    [Fact]
    public async Task GenerateSummaryAsync_UnrecognizedFields_FallbackListsAll()
    {
        var fields = new Dictionary<string, string>
        {
            { "Foo", "Bar" },
            { "Baz", "Qux" }
        };

        var result = await _adapter.GenerateSummaryAsync(fields);

        Assert.True(result.IsSuccess);
        Assert.Contains("Foo: Bar", result.Value);
        Assert.Contains("Baz: Qux", result.Value);
    }

    [Fact]
    public async Task GenerateSummaryAsync_MultipleKnownFields_JoinsWithPeriod()
    {
        var fields = new Dictionary<string, string>
        {
            { "Name", "Alice" },
            { "Type", "Mental Health" },
            { "Age", "34" }
        };

        var result = await _adapter.GenerateSummaryAsync(fields);

        Assert.True(result.IsSuccess);
        Assert.Contains("Subject: Alice", result.Value);
        Assert.Contains("Category: Mental Health", result.Value);
        Assert.Contains("Age: 34", result.Value);
        Assert.EndsWith(".", result.Value);
    }

    [Fact]
    public async Task GenerateSummaryAsync_ResultEndsWithPeriod()
    {
        var fields = new Dictionary<string, string> { { "Name", "Bob" } };

        var result = await _adapter.GenerateSummaryAsync(fields);

        Assert.True(result.IsSuccess);
        Assert.EndsWith(".", result.Value);
    }
}
