using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using MovieSearchCase.Domain.Entities;
using MovieSearchCase.Domain.Interfaces.Services;

namespace MovieSearchCase.Infrastructure.Caching;

public sealed class CachedMovieService : IMovieService
{
    private readonly IMovieService _inner;
    private readonly HybridCache _cache;
    private readonly CachingOptions _options;
    private readonly ILogger<CachedMovieService> _logger;

    private readonly HybridCacheEntryOptions _trendingEntry;
    private readonly HybridCacheEntryOptions _searchEntry;
    private readonly HybridCacheEntryOptions _detailsEntry;
    private readonly HybridCacheEntryOptions _trailersEntry;

    public CachedMovieService(
        IMovieService inner,
        HybridCache cache,
        CachingOptions options,
        ILogger<CachedMovieService> logger)
    {
        _inner = inner;
        _cache = cache;
        _options = options;
        _logger = logger;

        _trendingEntry = HybridCacheExtensions.CreateEntryOptions(options.TrendingMinutes);
        _searchEntry = HybridCacheExtensions.CreateEntryOptions(options.SearchMinutes);
        _detailsEntry = HybridCacheExtensions.CreateEntryOptions(options.MovieDetailsMinutes);
        _trailersEntry = HybridCacheExtensions.CreateEntryOptions(options.TrailersMinutes);
    }

    public Task<IReadOnlyList<Movie>> GetTrendingAsync(CancellationToken cancellationToken) =>
        _cache.GetOrFetchAsync(
            CacheKeys.Trending,
            _inner,
            static (inner, token) => inner.GetTrendingAsync(token),
            _trendingEntry,
            _logger,
            cancellationToken);

    public Task<MovieSearchResponse<Movie>> SearchMoviesAsync(MovieSearchRequest request, CancellationToken cancellationToken)
    {
        // Blank or oversized queries are not worth a cache slot; let the real service deal with them.
        if (!CacheKeys.TryMovieSearch(request, _options.MaxQueryLength, out var key))
        {
            return _inner.SearchMoviesAsync(request, cancellationToken);
        }

        return _cache.GetOrFetchAsync(
            key,
            (Inner: _inner, Request: request),
            static (state, token) => state.Inner.SearchMoviesAsync(state.Request, token),
            _searchEntry,
            _logger,
            cancellationToken);
    }

    public Task<MovieExtendDetails> GetMovieAsync(int movieId, CancellationToken cancellationToken) =>
        _cache.GetOrFetchAsync(
            CacheKeys.MovieDetails(movieId),
            (Inner: _inner, MovieId: movieId),
            static (state, token) => state.Inner.GetMovieAsync(state.MovieId, token),
            _detailsEntry,
            _logger,
            cancellationToken);

    public Task<IReadOnlyList<MovieTrailerVideoKeys>> GetTrailerVideoKeysAsync(int movieId, CancellationToken cancellationToken) =>
        _cache.GetOrFetchAsync(
            CacheKeys.MovieTrailers(movieId),
            (Inner: _inner, MovieId: movieId),
            static (state, token) => state.Inner.GetTrailerVideoKeysAsync(state.MovieId, token),
            _trailersEntry,
            _logger,
            cancellationToken);
}