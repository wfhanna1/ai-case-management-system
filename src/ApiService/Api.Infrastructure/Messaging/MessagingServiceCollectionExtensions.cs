using Api.Domain.Ports;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace Api.Infrastructure.Messaging;

/// <summary>
/// Registers MassTransit + RabbitMQ for the API service.
/// Configuration is read from the environment via IConfiguration (12-factor).
/// Required config keys: RabbitMQ__Host, RabbitMQ__Username, RabbitMQ__Password.
/// </summary>
public static class MessagingServiceCollectionExtensions
{
    public static IServiceCollection AddApiMessaging(
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
            bus.AddConsumer<DocumentProcessedConsumer>();

            bus.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.Host(host, h =>
                {
                    h.Username(username);
                    h.Password(password);
                });

                cfg.UsePublishFilter(typeof(TenantHeaderPublishFilter<>), ctx);

                cfg.ReceiveEndpoint("api-document-processed", ep =>
                {
                    ep.UseMessageRetry(r =>
                        r.Exponential(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(2)));

                    ep.ConfigureConsumer<DocumentProcessedConsumer>(ctx);
                });
            });
        });

        services.AddTransient<IMessageBusPort, MassTransitMessageBusAdapter>();

        return services;
    }
}
