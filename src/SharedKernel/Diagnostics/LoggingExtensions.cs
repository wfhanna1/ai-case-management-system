using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;

namespace SharedKernel.Diagnostics;

public static class LoggingExtensions
{
    public static ILoggingBuilder AddStructuredConsoleLogging(this ILoggingBuilder logging)
    {
        logging.ClearProviders();
        logging.AddJsonConsole(options =>
        {
            options.IncludeScopes = true;
            options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
        });
        logging.AddOpenTelemetry(otel =>
        {
            otel.IncludeScopes = true;
            otel.IncludeFormattedMessage = true;
        });

        // Prevent EF Core and ASP.NET from logging SQL parameters, tokens, or
        // other sensitive data at lower log levels.
        logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
        logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);

        return logging;
    }
}
