using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RagService.Domain.Ports;

namespace RagService.Infrastructure.Embeddings;

public static class EmbeddingServiceCollectionExtensions
{
    public static IServiceCollection AddEmbeddingAdapter(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var settings = configuration.GetSection("Embedding").Get<EmbeddingSettings>()
            ?? new EmbeddingSettings();

        if (string.Equals(settings.Provider, "mock", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<IEmbeddingPort, MockEmbeddingAdapter>();
        else
            services.AddSingleton<IEmbeddingPort, LocalEmbeddingAdapter>();

        return services;
    }
}
