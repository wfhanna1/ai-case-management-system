namespace Api.Application.DTOs;

public sealed record SubmitDocumentRequest(
    Guid TenantId,
    string FileName,
    string ContentType);
