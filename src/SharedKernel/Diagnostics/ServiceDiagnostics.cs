using System.Diagnostics;

namespace SharedKernel.Diagnostics;

/// <summary>
/// Holds an ActivitySource for a service, used for distributed tracing.
/// Each microservice creates one instance with its service name.
/// Uses only System.Diagnostics -- no OpenTelemetry dependency in SharedKernel.
/// </summary>
public sealed class ServiceDiagnostics : IDisposable
{
    public string ServiceName { get; }
    public ActivitySource ActivitySource { get; }

    public ServiceDiagnostics(string serviceName)
    {
        ServiceName = serviceName;
        ActivitySource = new ActivitySource(serviceName);
    }

    public void Dispose() => ActivitySource.Dispose();
}
