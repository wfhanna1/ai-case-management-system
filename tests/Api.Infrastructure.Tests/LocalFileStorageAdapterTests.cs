using Api.Infrastructure.Storage;
using SharedKernel;

namespace Api.Infrastructure.Tests;

public sealed class LocalFileStorageAdapterTests : IDisposable
{
    private readonly string _tempDir;
    private readonly LocalFileStorageAdapter _adapter;
    private readonly TenantId _tenantId = TenantId.New();

    public LocalFileStorageAdapterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"storage-test-{Guid.NewGuid()}");
        _adapter = new LocalFileStorageAdapter(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task UploadAsync_StoresFileAtExpectedPath()
    {
        using var stream = new MemoryStream("hello"u8.ToArray());

        var result = await _adapter.UploadAsync(stream, "doc.pdf", "application/pdf", _tenantId);

        Assert.True(result.IsSuccess);
        var fullPath = Path.Combine(_tempDir, result.Value);
        Assert.True(File.Exists(fullPath));
        Assert.Equal("hello", await File.ReadAllTextAsync(fullPath));
    }

    [Fact]
    public async Task DownloadAsync_ReturnsFileStream()
    {
        using var upload = new MemoryStream("content"u8.ToArray());
        var uploadResult = await _adapter.UploadAsync(upload, "read.pdf", "application/pdf", _tenantId);

        var result = await _adapter.DownloadAsync(uploadResult.Value, _tenantId);

        Assert.True(result.IsSuccess);
        using var reader = new StreamReader(result.Value);
        Assert.Equal("content", await reader.ReadToEndAsync());
        result.Value.Dispose();
    }

    [Fact]
    public async Task DeleteAsync_RemovesFile()
    {
        using var upload = new MemoryStream("delete-me"u8.ToArray());
        var uploadResult = await _adapter.UploadAsync(upload, "gone.pdf", "application/pdf", _tenantId);
        var fullPath = Path.Combine(_tempDir, uploadResult.Value);
        Assert.True(File.Exists(fullPath));

        var result = await _adapter.DeleteAsync(uploadResult.Value, _tenantId);

        Assert.True(result.IsSuccess);
        Assert.False(File.Exists(fullPath));
    }

    [Fact]
    public async Task ResolveSafePath_PathTraversal_ReturnsFailure()
    {
        var result = await _adapter.DownloadAsync("../../etc/passwd", _tenantId);

        Assert.True(result.IsFailure);
        Assert.Equal("STORAGE_ERROR", result.Error.Code);
    }
}
