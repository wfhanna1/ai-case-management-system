using Microsoft.Extensions.Logging;

namespace OcrWorker.Tests;

/// <summary>
/// Verifies that the ScopeCapturingLogger utility works as expected.
/// The actual consumer log scope is exercised by the existing consumer tests
/// via NullLogger, which proves BeginScope does not throw.
/// </summary>
public sealed class ScopeCapturingLoggerTests
{
    [Fact]
    public void BeginScope_CapturesState()
    {
        var logger = new ScopeCapturingLogger<string>();
        var state = new Dictionary<string, object?>
        {
            ["TenantId"] = Guid.NewGuid(),
            ["DocumentId"] = Guid.NewGuid(),
            ["TraceId"] = "abc123"
        };

        using (logger.BeginScope(state))
        {
            logger.LogInformation("test message");
        }

        Assert.Single(logger.Scopes);
        var captured = logger.Scopes[0] as IReadOnlyDictionary<string, object?>;
        Assert.NotNull(captured);
        Assert.True(captured.ContainsKey("TenantId"));
        Assert.True(captured.ContainsKey("DocumentId"));
        Assert.True(captured.ContainsKey("TraceId"));
    }

    [Fact]
    public void BeginScope_MultipleScopes_CapturedInOrder()
    {
        var logger = new ScopeCapturingLogger<string>();

        using (logger.BeginScope("scope1"))
        using (logger.BeginScope("scope2"))
        {
            logger.LogInformation("test");
        }

        Assert.Equal(2, logger.Scopes.Count);
        Assert.Equal("scope1", logger.Scopes[0]);
        Assert.Equal("scope2", logger.Scopes[1]);
    }
}

/// <summary>
/// A test logger that captures BeginScope state for verification.
/// Used across test projects to verify log enrichment in middleware and consumers.
/// </summary>
public sealed class ScopeCapturingLogger<T> : ILogger<T>
{
    public List<object?> Scopes { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        Scopes.Add(state);
        return NullScope.Instance;
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter) { }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}
