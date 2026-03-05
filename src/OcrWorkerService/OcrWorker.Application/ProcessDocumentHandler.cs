using OcrWorker.Domain.Ports;
using SharedKernel;

namespace OcrWorker.Application;

/// <summary>
/// Orchestrates document processing: fetches the file, runs OCR, and returns extracted fields.
/// </summary>
public sealed class ProcessDocumentHandler
{
    private readonly IOcrPort _ocrPort;

    public ProcessDocumentHandler(IOcrPort ocrPort)
    {
        _ocrPort = ocrPort;
    }

    public async Task<Result<OcrResult>> HandleAsync(
        Stream documentContent,
        string fileName,
        CancellationToken ct = default)
    {
        return await _ocrPort.ExtractTextAsync(documentContent, fileName, ct);
    }
}
