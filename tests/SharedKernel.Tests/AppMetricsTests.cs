using SharedKernel.Diagnostics;

namespace SharedKernel.Tests;

public sealed class AppMetricsTests
{
    [Fact]
    public void Constructor_CreatesAllCountersAndHistograms()
    {
        var metrics = new AppMetrics("TestService");

        Assert.NotNull(metrics.DocumentsSubmitted);
        Assert.NotNull(metrics.DocumentsReviewed);
        Assert.NotNull(metrics.ReviewsApproved);
        Assert.NotNull(metrics.ReviewsCorrected);
        Assert.NotNull(metrics.OcrSuccessCount);
        Assert.NotNull(metrics.OcrFailureCount);
        Assert.NotNull(metrics.EmbeddingsGenerated);
        Assert.NotNull(metrics.EmbeddingFailures);
        Assert.NotNull(metrics.OcrProcessingDuration);
    }

    [Fact]
    public void Counters_CanBeIncremented_WithoutError()
    {
        var metrics = new AppMetrics("TestService");

        metrics.DocumentsSubmitted.Add(1);
        metrics.DocumentsReviewed.Add(1);
        metrics.ReviewsApproved.Add(1);
        metrics.ReviewsCorrected.Add(1);
        metrics.OcrSuccessCount.Add(1);
        metrics.OcrFailureCount.Add(1);
        metrics.EmbeddingsGenerated.Add(1);
        metrics.EmbeddingFailures.Add(1);
    }

    [Fact]
    public void Histogram_CanRecordValues_WithoutError()
    {
        var metrics = new AppMetrics("TestService");

        metrics.OcrProcessingDuration.Record(1234.5);
        metrics.OcrProcessingDuration.Record(0.1);
    }
}
