using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OcrWorker.Domain.Ports;
using SharedKernel;

namespace OcrWorker.Infrastructure.Storage;

/// <summary>
/// Reads files from the local filesystem (shared volume with the API service).
/// Base path is configured via FileStorage:BasePath.
/// </summary>
public sealed class LocalFileStorageReadAdapter : IFileStorageReadPort
{
    private readonly string _basePath;
    private readonly ILogger<LocalFileStorageReadAdapter> _logger;

    public LocalFileStorageReadAdapter(IConfiguration configuration, ILogger<LocalFileStorageReadAdapter> logger)
    {
        _basePath = configuration["FileStorage:BasePath"]
            ?? throw new InvalidOperationException("FileStorage:BasePath is not configured.");
        _logger = logger;
    }

    public Task<Result<Stream>> DownloadAsync(string storageKey, CancellationToken ct = default)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_basePath, storageKey));
        var baseFull = Path.GetFullPath(_basePath);

        if (!fullPath.StartsWith(baseFull, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Path traversal attempt detected for storage key {StorageKey}", storageKey);
            return Task.FromResult(Result<Stream>.Failure(
                new Error("INVALID_PATH", "Storage key resolves outside the base path.")));
        }

        if (!File.Exists(fullPath))
        {
            _logger.LogWarning("File not found at {Path} for storage key {StorageKey}", fullPath, storageKey);
            return Task.FromResult(Result<Stream>.Failure(
                new Error("FILE_NOT_FOUND", $"File not found for storage key: {storageKey}")));
        }

        try
        {
            Stream stream = File.OpenRead(fullPath);
            return Task.FromResult(Result<Stream>.Success(stream));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read file at {Path}", fullPath);
            return Task.FromResult(Result<Stream>.Failure(
                new Error("FILE_READ_ERROR", $"Failed to read file: {ex.Message}")));
        }
    }
}
