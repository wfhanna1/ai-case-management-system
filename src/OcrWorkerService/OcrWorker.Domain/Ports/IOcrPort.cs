using SharedKernel;

namespace OcrWorker.Domain.Ports;

/// <summary>
/// Port interface for OCR processing. Implementations adapt to specific OCR engines
/// (Azure Document Intelligence, Tesseract, etc.).
/// </summary>
public interface IOcrPort
{
    Task<Result<OcrResult>> ExtractTextAsync(
        Stream documentContent,
        string fileName,
        CancellationToken ct = default);
}

public sealed record OcrResult(
    string RawText,
    Dictionary<string, ExtractedField> Fields);

public sealed record ExtractedField(
    string FieldName,
    string Value,
    double Confidence);
