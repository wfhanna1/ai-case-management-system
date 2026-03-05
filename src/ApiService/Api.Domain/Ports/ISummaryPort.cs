using SharedKernel;

namespace Api.Domain.Ports;

/// <summary>
/// Port interface for generating case summaries from extracted fields.
/// Implementations adapt to LLM APIs (OpenAI, etc.) or template-based approaches.
/// </summary>
public interface ISummaryPort
{
    Task<Result<string>> GenerateSummaryAsync(
        Dictionary<string, string> fields,
        CancellationToken ct = default);
}
