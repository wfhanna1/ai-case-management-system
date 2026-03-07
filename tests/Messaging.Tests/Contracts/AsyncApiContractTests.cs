using System.Reflection;
using Messaging.Contracts.Events;
using Messaging.Contracts.Models;
using YamlDotNet.Serialization;


namespace Messaging.Tests.Contracts;

/// <summary>
/// Validates that C# message contract records stay in sync with the schemas
/// defined in contracts/messaging.asyncapi.yaml. If a property is added or
/// removed from either side without updating the other, these tests will fail.
/// </summary>
public sealed class AsyncApiContractTests
{
    private static readonly Dictionary<object, object> Spec;

    static AsyncApiContractTests()
    {
        var root = GetSolutionRoot();
        var yamlPath = Path.Combine(root, "contracts", "messaging.asyncapi.yaml");
        var yaml = File.ReadAllText(yamlPath);

        var deserializer = new DeserializerBuilder()
            .Build();

        Spec = deserializer.Deserialize<Dictionary<object, object>>(yaml);
    }

    [Theory]
    [InlineData("DocumentUploadedEvent", typeof(DocumentUploadedEvent))]
    [InlineData("DocumentProcessedEvent", typeof(DocumentProcessedEvent))]
    [InlineData("EmbeddingRequestedEvent", typeof(EmbeddingRequestedEvent))]
    [InlineData("EmbeddingCompletedEvent", typeof(EmbeddingCompletedEvent))]
    [InlineData("ExtractedFieldResult", typeof(ExtractedFieldResult))]
    public void Schema_Properties_Match_CSharp_Record(string schemaName, Type recordType)
    {
        var schemaProperties = GetSchemaPropertyNames(schemaName);
        var csharpProperties = recordType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => ToCamelCase(p.Name))
            .ToHashSet();

        // Every YAML property must exist on the C# type
        foreach (var yamlProp in schemaProperties)
        {
            Assert.True(
                csharpProperties.Contains(yamlProp),
                $"Schema '{schemaName}' defines property '{yamlProp}' but the C# record does not have it.");
        }

        // Every C# property must exist in the YAML schema
        foreach (var csProp in csharpProperties)
        {
            Assert.True(
                schemaProperties.Contains(csProp),
                $"C# record '{recordType.Name}' has property '{csProp}' but the schema does not define it.");
        }
    }

    [Theory]
    [InlineData("DocumentUploadedEvent", new[] { "documentId", "tenantId", "fileName", "storageKey", "uploadedAt" })]
    [InlineData("DocumentProcessedEvent", new[] { "documentId", "tenantId", "extractedFields", "processedAt" })]
    [InlineData("EmbeddingRequestedEvent", new[] { "documentId", "tenantId", "textContent", "fieldValues", "requestedAt" })]
    [InlineData("EmbeddingCompletedEvent", new[] { "documentId", "tenantId", "completedAt" })]
    public void Schema_Required_Fields_Are_Correct(string schemaName, string[] expectedRequired)
    {
        var schema = GetSchema(schemaName);
        var requiredNode = (List<object>)schema["required"];
        var actualRequired = requiredNode.Cast<string>().OrderBy(s => s).ToList();
        var expected = expectedRequired.OrderBy(s => s).ToList();

        Assert.Equal(expected, actualRequired);
    }

    [Fact]
    public void All_Channels_Defined()
    {
        var channels = (Dictionary<object, object>)Spec["channels"];
        var channelNames = channels.Keys.Cast<string>().ToHashSet();

        Assert.Contains("ocrworker-document-uploaded", channelNames);
        Assert.Contains("apiservice-document-processed", channelNames);
        Assert.Contains("ragservice-embedding-requested", channelNames);
        Assert.Contains("apiservice-embedding-completed", channelNames);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static HashSet<string> GetSchemaPropertyNames(string schemaName)
    {
        var schema = GetSchema(schemaName);
        var properties = (Dictionary<object, object>)schema["properties"];
        return properties.Keys.Cast<string>().ToHashSet();
    }

    private static Dictionary<object, object> GetSchema(string schemaName)
    {
        var components = (Dictionary<object, object>)Spec["components"];
        var schemas = (Dictionary<object, object>)components["schemas"];
        return (Dictionary<object, object>)schemas[schemaName];
    }

    private static string ToCamelCase(string pascalCase)
    {
        if (string.IsNullOrEmpty(pascalCase))
            return pascalCase;

        return char.ToLowerInvariant(pascalCase[0]) + pascalCase[1..];
    }

    private static string GetSolutionRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "IntakeDocumentProcessor.sln")))
                return dir;

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new InvalidOperationException(
            "Could not find IntakeDocumentProcessor.sln walking up from " + AppContext.BaseDirectory);
    }
}
