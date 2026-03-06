using System.Text;
using OcrWorker.Application;
using OcrWorker.Domain.Ports;
using SharedKernel;

namespace OcrWorker.Tests;

public sealed class ProcessDocumentHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenOcrPortReturnsSuccess_ReturnsSuccess()
    {
        var expectedFields = new Dictionary<string, ExtractedField>
        {
            ["ClientName"] = new("ClientName", "Alice Johnson", 0.95)
        };
        var expectedResult = new OcrResult("Full raw text from document.", expectedFields);
        var ocrPort = new StubOcrPort(Result<OcrResult>.Success(expectedResult));
        var sut = new ProcessDocumentHandler(ocrPort);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("pdf bytes"));
        var result = await sut.HandleAsync(stream, "intake-form.pdf");

        Assert.True(result.IsSuccess);
        Assert.Equal("Full raw text from document.", result.Value.RawText);
        Assert.Single(result.Value.Fields);
        Assert.Equal("Alice Johnson", result.Value.Fields["ClientName"].Value);
        Assert.Equal(0.95, result.Value.Fields["ClientName"].Confidence);
    }

    [Fact]
    public async Task HandleAsync_WhenOcrPortReturnsFailure_PropagatesFailure()
    {
        var error = new Error("OCR_ERROR", "Unable to process document");
        var ocrPort = new StubOcrPort(Result<OcrResult>.Failure(error));
        var sut = new ProcessDocumentHandler(ocrPort);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("bad data"));
        var result = await sut.HandleAsync(stream, "corrupt.pdf");

        Assert.True(result.IsFailure);
        Assert.Equal("OCR_ERROR", result.Error.Code);
        Assert.Equal("Unable to process document", result.Error.Message);
    }

    // --- Test double ---

    private sealed class StubOcrPort : IOcrPort
    {
        private readonly Result<OcrResult> _result;

        public StubOcrPort(Result<OcrResult> result) => _result = result;

        public Task<Result<OcrResult>> ExtractTextAsync(
            Stream documentContent, string fileName, CancellationToken ct = default)
        {
            return Task.FromResult(_result);
        }
    }
}
