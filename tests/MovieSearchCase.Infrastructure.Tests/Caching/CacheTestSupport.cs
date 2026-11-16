using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace MovieSearchCase.Infrastructure.Tests.Caching;

/// <summary>
/// Owns a real, isolated in-memory <see cref="HybridCache"/> so the cached decorators are tested
/// against the actual caching behaviour (hits, misses, stampede protection, failure handling)
/// rather than a mock. Every test class instance gets a fresh cache.
/// </summary>
internal sealed class HybridCacheHost : IDisposable
{
    private readonly ServiceProvider _provider;

    public HybridCache Cache { get; }

    public HybridCacheHost()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHybridCache();
        _provider = services.BuildServiceProvider();
        Cache = _provider.GetRequiredService<HybridCache>();
    }

    public void Dispose() => _provider.Dispose();
}
