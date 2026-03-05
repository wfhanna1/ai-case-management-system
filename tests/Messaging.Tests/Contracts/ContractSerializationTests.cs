using System.Reflection;
using System.Text.Json;
using Messaging.Contracts.Events;
using Messaging.Contracts.Models;

namespace Messaging.Tests.Contracts;

/// <summary>
/// Verifies that all message contracts round-trip through System.Text.Json without data loss.
/// MassTransit uses JSON serialization over the wire so this guards against silent failures.
/// </summary>
public sealed class ContractSerializationTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void DocumentUploadedEvent_RoundTrips_WithAllFields()
    {
        var original = new DocumentUploadedEvent(
            DocumentId: Guid.NewGuid(),
            TemplateId: Guid.NewGuid(),
            TenantId: Guid.NewGuid(),
            FileName: "intake-2024.pdf",
            UploadedAt: new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero));

        var json = JsonSerializer.Serialize(original, SerializerOptions);
        var deserialized = JsonSerializer.Deserialize<DocumentUploadedEvent>(json, SerializerOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(original.DocumentId, deserialized.DocumentId);
        Assert.Equal(original.TemplateId, deserialized.TemplateId);
        Assert.Equal(original.TenantId, deserialized.TenantId);
        Assert.Equal(original.FileName, deserialized.FileName);
        Assert.Equal(original.UploadedAt, deserialized.UploadedAt);
    }

    [Fact]
    public void DocumentProcessedEvent_RoundTrips_WithExtractedFields()
    {
        var fields = new Dictionary<string, ExtractedFieldResult>
        {
            ["PatientName"] = new ExtractedFieldResult("PatientName", "Jane Doe", 0.97),
            ["DateOfBirth"] = new ExtractedFieldResult("DateOfBirth", "1985-06-20", 0.88)
        };

        var original = new DocumentProcessedEvent(
            DocumentId: Guid.NewGuid(),
            TenantId: Guid.NewGuid(),
            ExtractedFields: fields,
            ProcessedAt: new DateTimeOffset(2024, 1, 15, 11, 0, 0, TimeSpan.Zero));

        var json = JsonSerializer.Serialize(original, SerializerOptions);
        var deserialized = JsonSerializer.Deserialize<DocumentProcessedEvent>(json, SerializerOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(original.DocumentId, deserialized.DocumentId);
        Assert.Equal(original.TenantId, deserialized.TenantId);
        Assert.Equal(2, deserialized.ExtractedFields.Count);
        Assert.Equal("Jane Doe", deserialized.ExtractedFields["PatientName"].Value);
        Assert.Equal(0.97, deserialized.ExtractedFields["PatientName"].Confidence);
        Assert.Equal(original.ProcessedAt, deserialized.ProcessedAt);
    }

    [Fact]
    public void EmbeddingRequestedEvent_RoundTrips_WithFieldValues()
    {
        var fieldValues = new Dictionary<string, string>
        {
            ["PatientName"] = "Jane Doe",
            ["Condition"] = "Hypertension"
        };

        var original = new EmbeddingRequestedEvent(
            DocumentId: Guid.NewGuid(),
            TenantId: Guid.NewGuid(),
            TextContent: "Patient Jane Doe presents with hypertension.",
            FieldValues: fieldValues,
            RequestedAt: new DateTimeOffset(2024, 1, 15, 11, 5, 0, TimeSpan.Zero));

        var json = JsonSerializer.Serialize(original, SerializerOptions);
        var deserialized = JsonSerializer.Deserialize<EmbeddingRequestedEvent>(json, SerializerOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(original.DocumentId, deserialized.DocumentId);
        Assert.Equal(original.TextContent, deserialized.TextContent);
        Assert.Equal(2, deserialized.FieldValues.Count);
        Assert.Equal("Jane Doe", deserialized.FieldValues["PatientName"]);
    }

    [Fact]
    public void EmbeddingCompletedEvent_RoundTrips_WithAllFields()
    {
        var original = new EmbeddingCompletedEvent(
            DocumentId: Guid.NewGuid(),
            TenantId: Guid.NewGuid(),
            CompletedAt: new DateTimeOffset(2024, 1, 15, 11, 10, 0, TimeSpan.Zero));

        var json = JsonSerializer.Serialize(original, SerializerOptions);
        var deserialized = JsonSerializer.Deserialize<EmbeddingCompletedEvent>(json, SerializerOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(original.DocumentId, deserialized.DocumentId);
        Assert.Equal(original.TenantId, deserialized.TenantId);
        Assert.Equal(original.CompletedAt, deserialized.CompletedAt);
    }

    [Fact]
    public void ExtractedFieldResult_RoundTrips_Correctly()
    {
        var original = new ExtractedFieldResult("PatientName", "John Smith", 0.994);

        var json = JsonSerializer.Serialize(original, SerializerOptions);
        var deserialized = JsonSerializer.Deserialize<ExtractedFieldResult>(json, SerializerOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(original.FieldName, deserialized.FieldName);
        Assert.Equal(original.Value, deserialized.Value);
        Assert.Equal(original.Confidence, deserialized.Confidence);
    }

    // -----------------------------------------------------------------------
    // Schema validation tests -- verify each contract has the required fields
    // so producers and consumers stay in sync across service boundaries.
    // -----------------------------------------------------------------------

    [Fact]
    public void DocumentUploadedEvent_Has_Required_Properties()
    {
        var properties = typeof(DocumentUploadedEvent).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var names = properties.Select(p => p.Name).ToHashSet();

        Assert.Contains("DocumentId", names);
        Assert.Contains("TenantId", names);
        Assert.Contains("FileName", names);
        Assert.Contains("UploadedAt", names);
    }

    [Fact]
    public void DocumentUploadedEvent_Property_Types_Match_Contract()
    {
        var props = typeof(DocumentUploadedEvent).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(p => p.Name, p => p.PropertyType);

        Assert.Equal(typeof(Guid), props["DocumentId"]);
        Assert.Equal(typeof(Guid), props["TenantId"]);
        Assert.Equal(typeof(string), props["FileName"]);
        Assert.Equal(typeof(DateTimeOffset), props["UploadedAt"]);
    }

    [Fact]
    public void DocumentProcessedEvent_Has_Required_Properties()
    {
        var names = typeof(DocumentProcessedEvent)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name).ToHashSet();

        Assert.Contains("DocumentId", names);
        Assert.Contains("TenantId", names);
        Assert.Contains("ExtractedFields", names);
        Assert.Contains("ProcessedAt", names);
    }

    [Fact]
    public void EmbeddingRequestedEvent_Has_Required_Properties()
    {
        var names = typeof(EmbeddingRequestedEvent)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name).ToHashSet();

        Assert.Contains("DocumentId", names);
        Assert.Contains("TenantId", names);
        Assert.Contains("TextContent", names);
        Assert.Contains("FieldValues", names);
        Assert.Contains("RequestedAt", names);
    }

    [Fact]
    public void EmbeddingCompletedEvent_Has_Required_Properties()
    {
        var names = typeof(EmbeddingCompletedEvent)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name).ToHashSet();

        Assert.Contains("DocumentId", names);
        Assert.Contains("TenantId", names);
        Assert.Contains("CompletedAt", names);
    }

    [Fact]
    public void ExtractedFieldResult_Has_Required_Properties()
    {
        var props = typeof(ExtractedFieldResult)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(p => p.Name, p => p.PropertyType);

        Assert.Contains("FieldName", props.Keys);
        Assert.Contains("Value", props.Keys);
        Assert.Contains("Confidence", props.Keys);

        Assert.Equal(typeof(string), props["FieldName"]);
        Assert.Equal(typeof(string), props["Value"]);
        Assert.Equal(typeof(double), props["Confidence"]);
    }
}
