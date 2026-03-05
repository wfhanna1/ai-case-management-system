using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RagService.Infrastructure.Messaging;

/// <summary>
/// Registers MassTransit + RabbitMQ for the RAG service.
/// Binds EmbeddingRequestedConsumer to its queue with retry and dead-letter configuration.
/// Required config keys: RabbitMQ__Host, RabbitMQ__Username, RabbitMQ__Password.
/// </summary>
public static class MessagingServiceCollectionExtensions
{
    public static IServiceCollection AddRagMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var host = configuration["RabbitMQ__Host"]
            ?? configuration["RabbitMQ:Host"]
            ?? throw new InvalidOperationException("Missing required configuration: RabbitMQ__Host");

        var username = configuration["RabbitMQ__Username"]
            ?? configuration["RabbitMQ:Username"]
            ?? throw new InvalidOperationException("Missing required configuration: RabbitMQ__Username");

        var password = configuration["RabbitMQ__Password"]
            ?? configuration["RabbitMQ:Password"]
            ?? throw new InvalidOperationException("Missing required configuration: RabbitMQ__Password");

        services.AddMassTransit(bus =>
        {
            bus.AddConsumer<EmbeddingRequestedConsumer>();

            bus.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.Host(host, h =>
                {
                    h.Username(username);
                    h.Password(password);
                });

                cfg.ReceiveEndpoint("rag-embedding-requested", ep =>
                {
                    // Retry 3 times with exponential backoff before moving to dead-letter queue.
                    // MassTransit + RabbitMQ automatically routes final failures to the _error queue.
                    ep.UseMessageRetry(r =>
                        r.Exponential(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(2)));

                    ep.ConfigureConsumer<EmbeddingRequestedConsumer>(ctx);
                });
            });
        });

        return services;
    }
}
