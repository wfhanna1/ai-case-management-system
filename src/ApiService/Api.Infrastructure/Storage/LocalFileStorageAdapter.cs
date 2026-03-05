using Api.Domain.Ports;
using SharedKernel;

namespace Api.Infrastructure.Storage;

/// <summary>
/// Local filesystem implementation of IFileStoragePort for development.
/// In production this would be replaced with Azure Blob, S3, etc.
/// </summary>
public sealed class LocalFileStorageAdapter : IFileStoragePort
{
    private readonly string _basePath;

    public LocalFileStorageAdapter(string basePath)
    {
        _basePath = basePath;
        Directory.CreateDirectory(_basePath);
    }

    private string ResolveSafePath(string storageKey)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_basePath, storageKey));
        var baseFull = Path.GetFullPath(_basePath + Path.DirectorySeparatorChar);
        if (!fullPath.StartsWith(baseFull, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Path traversal detected");
        return fullPath;
    }

    public async Task<Result<string>> UploadAsync(
        Stream content, string fileName, string contentType, TenantId tenantId, CancellationToken ct = default)
    {
        try
        {
            var safeFileName = Path.GetFileName(fileName);
            var storageKey = $"{tenantId.Value}/{Guid.NewGuid()}/{safeFileName}";
            var fullPath = ResolveSafePath(storageKey);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            await using var fileStream = File.Create(fullPath);
            await content.CopyToAsync(fileStream, ct);

            return Result<string>.Success(storageKey);
        }
        catch (Exception ex)
        {
            return Result<string>.Failure(new Error("STORAGE_ERROR", ex.Message));
        }
    }

    public Task<Result<Stream>> DownloadAsync(string storageKey, TenantId tenantId, CancellationToken ct = default)
    {
        try
        {
            var fullPath = ResolveSafePath(storageKey);
            if (!File.Exists(fullPath))
                return Task.FromResult(Result<Stream>.Failure(new Error("NOT_FOUND", $"File not found: {storageKey}")));

            Stream stream = File.OpenRead(fullPath);
            return Task.FromResult(Result<Stream>.Success(stream));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<Stream>.Failure(new Error("STORAGE_ERROR", ex.Message)));
        }
    }

    public Task<Result<Unit>> DeleteAsync(string storageKey, TenantId tenantId, CancellationToken ct = default)
    {
        try
        {
            var fullPath = ResolveSafePath(storageKey);
            if (File.Exists(fullPath))
                File.Delete(fullPath);

            return Task.FromResult(Result<Unit>.Success(Unit.Value));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<Unit>.Failure(new Error("STORAGE_ERROR", ex.Message)));
        }
    }
}
