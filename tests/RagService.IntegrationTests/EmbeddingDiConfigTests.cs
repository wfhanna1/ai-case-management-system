using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RagService.Domain.Ports;
using RagService.Infrastructure.Embeddings;

namespace RagService.IntegrationTests;

public sealed class EmbeddingDiConfigTests
{
    [Fact]
    public void Provider_Local_RegistersLocalEmbeddingAdapter()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Embedding:Provider"] = "local"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddEmbeddingAdapter(config);
        using var provider = services.BuildServiceProvider();

        var adapter = provider.GetRequiredService<IEmbeddingPort>();
        Assert.IsType<LocalEmbeddingAdapter>(adapter);
    }

    [Fact]
    public void Provider_Mock_RegistersMockEmbeddingAdapter()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Embedding:Provider"] = "mock"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddEmbeddingAdapter(config);
        using var provider = services.BuildServiceProvider();

        var adapter = provider.GetRequiredService<IEmbeddingPort>();
        Assert.IsType<MockEmbeddingAdapter>(adapter);
    }

    [Fact]
    public void Provider_Default_RegistersLocalEmbeddingAdapter()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { })
            .Build();

        var services = new ServiceCollection();
        services.AddEmbeddingAdapter(config);
        using var provider = services.BuildServiceProvider();

        var adapter = provider.GetRequiredService<IEmbeddingPort>();
        Assert.IsType<LocalEmbeddingAdapter>(adapter);
    }
}
