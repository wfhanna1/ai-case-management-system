using SharedKernel;

namespace OcrWorker.Domain.Ports;

/// <summary>
/// Read-only port for downloading documents from file storage.
/// The OcrWorker only needs to read files uploaded by the API service.
/// </summary>
public interface IFileStorageReadPort
{
    Task<Result<Stream>> DownloadAsync(string storageKey, CancellationToken ct = default);
}
