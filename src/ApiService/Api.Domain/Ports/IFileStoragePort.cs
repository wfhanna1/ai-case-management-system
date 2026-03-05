using SharedKernel;

namespace Api.Domain.Ports;

/// <summary>
/// Output port for storing and retrieving raw document files (e.g., scanned images, PDFs).
/// Implementations in Api.Infrastructure adapt to Azure Blob, S3, or local filesystem.
/// </summary>
public interface IFileStoragePort
{
    /// <summary>
    /// Stores a file and returns its storage key (URL or object key).
    /// </summary>
    Task<Result<string>> UploadAsync(
        Stream content,
        string fileName,
        string contentType,
        TenantId tenantId,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves a file by its storage key.
    /// </summary>
    Task<Result<Stream>> DownloadAsync(string storageKey, TenantId tenantId, CancellationToken ct = default);

    /// <summary>
    /// Permanently removes a file.
    /// </summary>
    Task<Result<Unit>> DeleteAsync(string storageKey, TenantId tenantId, CancellationToken ct = default);
}
