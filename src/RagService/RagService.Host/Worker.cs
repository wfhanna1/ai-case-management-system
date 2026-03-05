namespace RagService.Host;

/// <summary>
/// Background worker that consumes completed OCR results and indexes document text
/// into a vector store for retrieval-augmented generation (RAG) queries.
/// Implements graceful shutdown when CancellationToken is signalled (SIGTERM).
/// </summary>
public sealed class RagWorkerService : BackgroundService
{
    private readonly ILogger<RagWorkerService> _logger;

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    public RagWorkerService(ILogger<RagWorkerService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RAG worker starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await IndexNextBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in RAG worker poll cycle.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }

        _logger.LogInformation("RAG worker stopped.");
    }

    private Task IndexNextBatchAsync(CancellationToken ct)
    {
        // TODO: inject and invoke use case from RagService.Application.
        _logger.LogInformation("RAG index cycle at {Time}.", DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }
}
