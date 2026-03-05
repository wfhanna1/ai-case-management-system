namespace Api.Application.DTOs;

public sealed record SimilarCaseDto(
    Guid DocumentId,
    double Score,
    string Summary,
    Dictionary<string, string> Metadata);

public sealed record SimilarCasesResultDto(
    IReadOnlyList<SimilarCaseDto> Items);
