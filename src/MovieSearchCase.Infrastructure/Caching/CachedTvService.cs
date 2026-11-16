using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using MovieSearchCase.Domain.Entities;
using MovieSearchCase.Domain.Interfaces.Services;

namespace MovieSearchCase.Infrastructure.Caching;

public sealed class CachedTvService : ITvService
{
    private readonly ITvService _inner;
    private readonly HybridCache _cache;
    private readonly CachingOptions _options;
    private readonly ILogger<CachedTvService> _logger;
    private readonly HybridCacheEntryOptions _searchEntry;

    public CachedTvService(
        ITvService inner,
        HybridCache cache,
        CachingOptions options,
        ILogger<CachedTvService> logger)
    {
        _inner = inner;
        _cache = cache;
        _options = options;
        _logger = logger;

        _searchEntry = HybridCacheExtensions.CreateEntryOptions(options.SearchMinutes);
    }

    public Task<TvSearchResponse<Tv>> SearchTvAsync(TvSearchRequest request, CancellationToken cancellationToken)
    {
        if (!CacheKeys.TryTvSearch(request, _options.MaxQueryLength, out var key))
        {
            return _inner.SearchTvAsync(request, cancellationToken);
        }

        return _cache.GetOrFetchAsync(
            key,
            (Inner: _inner, Request: request),
            static (state, token) => state.Inner.SearchTvAsync(state.Request, token),
            _searchEntry,
            _logger,
            cancellationToken);
    }
}