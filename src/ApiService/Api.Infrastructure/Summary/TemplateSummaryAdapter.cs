using Api.Domain.Ports;
using SharedKernel;

namespace Api.Infrastructure.Summary;

/// <summary>
/// Template-based summary generator that builds human-readable summaries
/// from extracted field metadata. Replace with OpenAISummaryAdapter for production.
/// </summary>
public sealed class TemplateSummaryAdapter : ISummaryPort
{
    public Task<Result<string>> GenerateSummaryAsync(
        Dictionary<string, string> fields,
        CancellationToken ct = default)
    {
        if (fields.Count == 0)
            return Task.FromResult(Result<string>.Success("No fields available."));

        var parts = new List<string>();

        // Name fields
        var nameKey = fields.Keys.FirstOrDefault(k =>
            k.Contains("Name", StringComparison.OrdinalIgnoreCase));
        if (nameKey is not null)
            parts.Add($"Subject: {fields[nameKey]}");

        // Type/category
        var typeKey = fields.Keys.FirstOrDefault(k =>
            k.Contains("Type", StringComparison.OrdinalIgnoreCase) ||
            k.Contains("Category", StringComparison.OrdinalIgnoreCase));
        if (typeKey is not null)
            parts.Add($"Category: {fields[typeKey]}");

        // Reason/symptoms
        var reasonKey = fields.Keys.FirstOrDefault(k =>
            k.Contains("Reason", StringComparison.OrdinalIgnoreCase) ||
            k.Contains("Symptom", StringComparison.OrdinalIgnoreCase));
        if (reasonKey is not null)
            parts.Add($"Presenting issue: {fields[reasonKey]}");

        // Urgency
        var urgencyKey = fields.Keys.FirstOrDefault(k =>
            k.Contains("Urgency", StringComparison.OrdinalIgnoreCase) ||
            k.Contains("Priority", StringComparison.OrdinalIgnoreCase));
        if (urgencyKey is not null)
            parts.Add($"Urgency: {fields[urgencyKey]}");

        // Age/demographics
        var ageKey = fields.Keys.FirstOrDefault(k =>
            k.Contains("Age", StringComparison.OrdinalIgnoreCase));
        if (ageKey is not null)
            parts.Add($"Age: {fields[ageKey]}");

        // Safety concern
        var safetyKey = fields.Keys.FirstOrDefault(k =>
            k.Contains("Safety", StringComparison.OrdinalIgnoreCase));
        if (safetyKey is not null && fields[safetyKey].Equals("Yes", StringComparison.OrdinalIgnoreCase))
            parts.Add("Safety concern flagged");

        if (parts.Count == 0)
        {
            // Fallback: list all fields
            parts.AddRange(fields.Select(kv => $"{kv.Key}: {kv.Value}"));
        }

        return Task.FromResult(Result<string>.Success(string.Join(". ", parts) + "."));
    }
}
