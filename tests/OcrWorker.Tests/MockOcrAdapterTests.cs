using System.Diagnostics;
using System.Text;
using OcrWorker.Infrastructure.Ocr;

namespace OcrWorker.Tests;

public sealed class MockOcrAdapterTests
{
    private readonly MockOcrAdapter _sut = new();

    [Fact]
    public async Task ExtractTextAsync_ReturnsOcrResult_WithFields()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test content"));

        var result = await _sut.ExtractTextAsync(stream, "intake-form.pdf");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.NotEmpty(result.Value.Fields);
        Assert.NotEmpty(result.Value.RawText);
    }

    [Fact]
    public async Task ExtractTextAsync_FieldsHaveConfidenceInRange_0_3_To_0_99()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test content"));

        var result = await _sut.ExtractTextAsync(stream, "document.pdf");

        Assert.True(result.IsSuccess);
        foreach (var field in result.Value.Fields.Values)
        {
            Assert.InRange(field.Confidence, 0.3, 0.99);
        }
    }

    [Fact]
    public async Task ExtractTextAsync_ReturnsRawText()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test content"));

        var result = await _sut.ExtractTextAsync(stream, "report.pdf");

        Assert.True(result.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(result.Value.RawText));
    }

    [Fact]
    public async Task ExtractTextAsync_SimulatesDelay()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test content"));
        var stopwatch = Stopwatch.StartNew();

        await _sut.ExtractTextAsync(stream, "document.pdf");

        stopwatch.Stop();
        Assert.True(stopwatch.ElapsedMilliseconds >= 1000,
            $"Expected delay >= 1000ms but was {stopwatch.ElapsedMilliseconds}ms");
    }
}
