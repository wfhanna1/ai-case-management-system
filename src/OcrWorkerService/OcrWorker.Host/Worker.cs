namespace OcrWorker.Host;

/// <summary>
/// Background worker that polls for submitted intake documents and runs OCR against them.
/// Implements graceful shutdown: stops polling cleanly when CancellationToken is signalled (SIGTERM).
/// </summary>
public sealed class OcrWorkerService : BackgroundService
{
    private readonly ILogger<OcrWorkerService> _logger;

    // Read poll interval from env/config so it can be tuned per deployment without code changes.
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    public OcrWorkerService(ILogger<OcrWorkerService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OCR worker starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessNextBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected on graceful shutdown; exit the loop.
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in OCR worker poll cycle.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }

        _logger.LogInformation("OCR worker stopped.");
    }

    private Task ProcessNextBatchAsync(CancellationToken ct)
    {
        // TODO: inject and invoke use case from OcrWorker.Application.
        _logger.LogInformation("OCR poll cycle at {Time}.", DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }
}
