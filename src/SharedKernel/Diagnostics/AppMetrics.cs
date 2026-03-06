using System.Diagnostics.Metrics;

namespace SharedKernel.Diagnostics;

/// <summary>
/// Application-level metrics using System.Diagnostics.Metrics.
/// No OpenTelemetry dependency -- OTel reads these via AddMeter().
/// </summary>
public sealed class AppMetrics : IDisposable
{
    private readonly Meter _meter;

    public Counter<long> DocumentsSubmitted { get; }
    public Counter<long> DocumentsReviewed { get; }
    public Counter<long> ReviewsApproved { get; }
    public Counter<long> ReviewsCorrected { get; }
    public Counter<long> FieldsCorrected { get; }
    public Counter<long> OcrSuccessCount { get; }
    public Counter<long> OcrFailureCount { get; }
    public Counter<long> EmbeddingsGenerated { get; }
    public Counter<long> EmbeddingFailures { get; }
    public Histogram<double> OcrProcessingDuration { get; }

    public AppMetrics(string serviceName)
    {
        _meter = new Meter(serviceName);

        DocumentsSubmitted = _meter.CreateCounter<long>(
            "app.documents.submitted", "documents", "Total documents submitted");
        DocumentsReviewed = _meter.CreateCounter<long>(
            "app.documents.reviewed", "documents", "Total documents reviewed");
        ReviewsApproved = _meter.CreateCounter<long>(
            "app.reviews.approved", "reviews", "Total reviews approved without corrections");
        ReviewsCorrected = _meter.CreateCounter<long>(
            "app.reviews.corrected", "reviews", "Total reviews with field corrections");
        FieldsCorrected = _meter.CreateCounter<long>(
            "app.fields.corrected", "fields", "Total individual field corrections");
        OcrSuccessCount = _meter.CreateCounter<long>(
            "app.ocr.success", "operations", "Total successful OCR operations");
        OcrFailureCount = _meter.CreateCounter<long>(
            "app.ocr.failure", "operations", "Total failed OCR operations");
        EmbeddingsGenerated = _meter.CreateCounter<long>(
            "app.embeddings.generated", "operations", "Total embeddings generated");
        EmbeddingFailures = _meter.CreateCounter<long>(
            "app.embeddings.failure", "operations", "Total embedding failures");
        OcrProcessingDuration = _meter.CreateHistogram<double>(
            "app.ocr.duration", "ms", "OCR processing duration in milliseconds");
    }

    public void Dispose() => _meter.Dispose();
}
