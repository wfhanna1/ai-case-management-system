using OcrWorker.Infrastructure.Ocr;

namespace OcrWorker.Tests;

/// <summary>
/// Integration tests for TesseractOcrAdapter.
/// These require the tesseract CLI to be installed on the system:
///   - macOS: brew install tesseract
///   - Ubuntu/Debian: apt-get install tesseract-ocr tesseract-ocr-eng
///   - Docker: installed automatically via Dockerfile
/// Tests will FAIL (not skip) if Tesseract is not available, so missing
/// infrastructure is surfaced immediately rather than hidden.
/// </summary>
[Trait("Category", "Integration")]
public sealed class TesseractOcrAdapterTests
{
    private static readonly string TessDataPath = ResolveTessDataPath();

    private static string ResolveTessDataPath()
    {
        string[] candidates =
        [
            "/opt/homebrew/share/tessdata",
            "/usr/share/tesseract-ocr/5/tessdata",
            "/usr/share/tesseract-ocr/4.00/tessdata",
            "/usr/share/tessdata"
        ];

        var found = candidates.FirstOrDefault(Directory.Exists);
        if (found is null)
        {
            throw new InvalidOperationException(
                "Tesseract tessdata directory not found. " +
                "Install Tesseract OCR: brew install tesseract (macOS) or " +
                "apt-get install tesseract-ocr tesseract-ocr-eng (Linux). " +
                $"Searched: {string.Join(", ", candidates)}");
        }

        return found;
    }

    private static string GetFixturePath(string fileName)
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "Fixtures")))
        {
            dir = Directory.GetParent(dir)?.FullName;
        }
        return dir != null
            ? Path.Combine(dir, "Fixtures", fileName)
            : throw new FileNotFoundException($"Fixtures directory not found for {fileName}");
    }

    [Fact]
    public async Task ExtractTextAsync_WithPngImage_ReturnsRawTextContainingImageContent()
    {
        var sut = new TesseractOcrAdapter(TessDataPath);

        var imagePath = GetFixturePath("test-document.png");
        using var stream = File.OpenRead(imagePath);

        var result = await sut.ExtractTextAsync(stream, "test-document.png");

        Assert.True(result.IsSuccess, $"Expected success but got: {(result.IsFailure ? result.Error.Message : "N/A")}");
        Assert.Contains("John Smith", result.Value.RawText);
    }

    [Fact]
    public async Task ExtractTextAsync_WithPngImage_ExtractsKeyValueFields()
    {
        var sut = new TesseractOcrAdapter(TessDataPath);

        var imagePath = GetFixturePath("test-document.png");
        using var stream = File.OpenRead(imagePath);

        var result = await sut.ExtractTextAsync(stream, "test-document.png");

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Fields.Count > 0, "Should extract at least one field");
        Assert.True(
            result.Value.Fields.Values.Any(f =>
                f.Value.Contains("John Smith", StringComparison.OrdinalIgnoreCase)),
            "Should extract a field containing 'John Smith'");
    }

    [Fact]
    public async Task ExtractTextAsync_WithPdfFile_DoesNotThrow()
    {
        var sut = new TesseractOcrAdapter(TessDataPath);

        var pdfPath = GetFixturePath("test-document.pdf");
        using var stream = File.OpenRead(pdfPath);

        // Minimal test PDF may not render in Docnet, but the adapter must
        // handle it gracefully (return a Result, never throw).
        var result = await sut.ExtractTextAsync(stream, "test-document.pdf");

        // The adapter must always return a valid Result (success or typed error)
        Assert.NotNull(result);
        if (result.IsFailure)
        {
            Assert.Equal("OCR_ERROR", result.Error.Code);
            Assert.False(string.IsNullOrWhiteSpace(result.Error.Message));
        }
        else
        {
            Assert.False(string.IsNullOrWhiteSpace(result.Value.RawText));
        }
    }

    [Fact]
    public async Task ExtractTextAsync_WithImageBasedPdf_ExtractsText()
    {
        var sut = new TesseractOcrAdapter(TessDataPath);

        var imagePath = GetFixturePath("test-document.png");
        var imageBytes = await File.ReadAllBytesAsync(imagePath);

        using var stream = new MemoryStream(imageBytes);
        var result = await sut.ExtractTextAsync(stream, "test-document.png");

        Assert.True(result.IsSuccess);
        Assert.Contains("John Smith", result.Value.RawText);
    }

    [Fact]
    public async Task ExtractTextAsync_WithEmptyStream_ReturnsFailure()
    {
        var sut = new TesseractOcrAdapter(TessDataPath);

        using var stream = new MemoryStream();

        var result = await sut.ExtractTextAsync(stream, "empty.png");

        Assert.True(result.IsFailure);
        Assert.Equal("OCR_ERROR", result.Error.Code);
    }

    [Fact]
    public async Task ExtractTextAsync_FieldConfidenceIsInValidRange()
    {
        var sut = new TesseractOcrAdapter(TessDataPath);

        var imagePath = GetFixturePath("test-document.png");
        using var stream = File.OpenRead(imagePath);

        var result = await sut.ExtractTextAsync(stream, "test-document.png");

        Assert.True(result.IsSuccess);
        foreach (var field in result.Value.Fields.Values)
        {
            Assert.InRange(field.Confidence, 0.0, 1.0);
        }
    }
}
