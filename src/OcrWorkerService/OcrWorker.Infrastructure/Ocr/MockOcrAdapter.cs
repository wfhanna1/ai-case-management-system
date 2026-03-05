using OcrWorker.Domain.Ports;
using SharedKernel;

namespace OcrWorker.Infrastructure.Ocr;

public sealed class MockOcrAdapter : IOcrPort
{
    private static readonly Dictionary<string, string[]> FieldPatterns = new()
    {
        ["intake"] = ["ClientName", "DateOfBirth", "CaseNumber", "Address"],
        ["report"] = ["ReportTitle", "Author", "Date", "Summary"],
        ["form"] = ["FormType", "SubmittedBy", "SubmissionDate", "Status"],
    };

    private static readonly string[] DefaultFields = ["DocumentTitle", "Date", "Content"];

    public async Task<Result<OcrResult>> ExtractTextAsync(
        Stream documentContent,
        string fileName,
        CancellationToken ct = default)
    {
        await Task.Delay(Random.Shared.Next(1000, 3000), ct);

        var fieldNames = ResolveFields(fileName);
        var fields = new Dictionary<string, ExtractedField>();

        foreach (var fieldName in fieldNames)
        {
            var confidence = Random.Shared.NextDouble() * 0.69 + 0.3;
            fields[fieldName] = new ExtractedField(fieldName, $"Sample {fieldName} value", confidence);
        }

        var rawText = $"[Mock OCR] Extracted text from {fileName}. " +
                      $"Document contains {fields.Count} identified fields.";

        return Result<OcrResult>.Success(new OcrResult(rawText, fields));
    }

    private static string[] ResolveFields(string fileName)
    {
        var lowerName = fileName.ToLowerInvariant();

        foreach (var (pattern, fields) in FieldPatterns)
        {
            if (lowerName.Contains(pattern))
                return fields;
        }

        return DefaultFields;
    }
}
