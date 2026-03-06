using SharedKernel.Diagnostics;

namespace SharedKernel.Tests;

public sealed class ServiceDiagnosticsTests
{
    [Fact]
    public void Constructor_SetsServiceName()
    {
        var diag = new ServiceDiagnostics("TestService");

        Assert.Equal("TestService", diag.ServiceName);
    }

    [Fact]
    public void Constructor_CreatesActivitySource_WithMatchingName()
    {
        var diag = new ServiceDiagnostics("TestService");

        Assert.NotNull(diag.ActivitySource);
        Assert.Equal("TestService", diag.ActivitySource.Name);
    }
}
