using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using OcrWorker.Infrastructure.Storage;

namespace OcrWorker.Tests;

public sealed class LocalFileStorageReadAdapterTests
{
    private static LocalFileStorageReadAdapter CreateAdapter(string basePath)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FileStorage:BasePath"] = basePath
            })
            .Build();

        return new LocalFileStorageReadAdapter(config, NullLogger<LocalFileStorageReadAdapter>.Instance);
    }

    [Fact]
    public async Task DownloadAsync_WhenFileExists_ReturnsStreamWithContent()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "test.pdf");
        await File.WriteAllTextAsync(filePath, "PDF content here");

        try
        {
            var sut = CreateAdapter(tempDir);
            var result = await sut.DownloadAsync("test.pdf");

            Assert.True(result.IsSuccess);
            using var reader = new StreamReader(result.Value);
            var content = await reader.ReadToEndAsync();
            Assert.Equal("PDF content here", content);
            result.Value.Dispose();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task DownloadAsync_WhenFileDoesNotExist_ReturnsFailure()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var sut = CreateAdapter(tempDir);
            var result = await sut.DownloadAsync("nonexistent.pdf");

            Assert.True(result.IsFailure);
            Assert.Equal("FILE_NOT_FOUND", result.Error.Code);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task DownloadAsync_WithNestedStorageKey_ResolvesPath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var nestedDir = Path.Combine(tempDir, "tenants", "abc");
        Directory.CreateDirectory(nestedDir);
        await File.WriteAllTextAsync(Path.Combine(nestedDir, "doc.pdf"), "nested content");

        try
        {
            var sut = CreateAdapter(tempDir);
            var result = await sut.DownloadAsync("tenants/abc/doc.pdf");

            Assert.True(result.IsSuccess);
            using var reader = new StreamReader(result.Value);
            Assert.Equal("nested content", await reader.ReadToEndAsync());
            result.Value.Dispose();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task DownloadAsync_WithPathTraversal_ReturnsFailure()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var sut = CreateAdapter(tempDir);
            var result = await sut.DownloadAsync("../../etc/passwd");

            Assert.True(result.IsFailure);
            Assert.Equal("INVALID_PATH", result.Error.Code);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Constructor_WhenBasePathNotConfigured_Throws()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        Assert.Throws<InvalidOperationException>(() =>
            new LocalFileStorageReadAdapter(config, NullLogger<LocalFileStorageReadAdapter>.Instance));
    }
}
