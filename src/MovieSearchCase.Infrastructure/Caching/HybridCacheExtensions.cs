using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MovieSearchCase.Infrastructure.Caching;

internal static class HybridCacheExtensions
{
    public static HybridCacheEntryOptions CreateEntryOptions(int minutes)
    {
        var lifetime = TimeSpan.FromMinutes(minutes);

        return new HybridCacheEntryOptions
        {
            Expiration = lifetime,
            LocalCacheExpiration = lifetime,
        };
    }

    public static Task<T> GetOrFetchAsync<TState, T>(
        this HybridCache cache,
        string key,
        TState state,
        Func<TState, CancellationToken, Task<T>> fetch,
        HybridCacheEntryOptions entryOptions,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        return cache.GetOrCreateAsync(
                key,
                (state, fetch, logger, key),
                static async (s, token) =>
                {
                    s.logger.LogDebug("Cache miss for {CacheKey}; calling TMDB.", s.key);
                    return await s.fetch(s.state, token).ConfigureAwait(false);
                },
                entryOptions,
                cancellationToken: cancellationToken)
            .AsTask();
    }
}