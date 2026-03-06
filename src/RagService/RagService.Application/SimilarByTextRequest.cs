namespace RagService.Application;

public sealed record SimilarByTextRequest(string Text, Guid TenantId, int TopK = 5);
