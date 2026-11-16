using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using MovieSearchCase.Domain.Entities;

namespace MovieSearchCase.Infrastructure.Caching;

internal static class CacheKeys
{
    private const string Version = "v1";

    public const string Trending = Version + ":movie:trending";

    public static string MovieDetails(int movieId) =>
        string.Create(CultureInfo.InvariantCulture, $"{Version}:movie:{movieId}");

    public static string MovieTrailers(int movieId) =>
        string.Create(CultureInfo.InvariantCulture, $"{Version}:movie:{movieId}:videos");

    public static bool TryMovieSearch(MovieSearchRequest request, int maxQueryLength, [NotNullWhen(true)] out string? key)
    {
        if (!TryNormalize(request.query, maxQueryLength, out var query))
        {
            key = null;
            return false;
        }

        key = string.Create(CultureInfo.InvariantCulture, $"{Version}:movie:search:{query}:{request.page}");
        return true;
    }

    public static bool TryTvSearch(TvSearchRequest request, int maxQueryLength, [NotNullWhen(true)] out string? key)
    {
        if (!TryNormalize(request.query, maxQueryLength, out var query))
        {
            key = null;
            return false;
        }

        var year = request.firstAirDateYear?.ToString(CultureInfo.InvariantCulture) ?? "any";
        key = string.Create(CultureInfo.InvariantCulture, $"{Version}:tv:search:{query}:{request.page}:{year}");
        return true;
    }

    internal static bool TryNormalize(string? query, int maxLength, [NotNullWhen(true)] out string? normalized)
    {
        normalized = null;

        if (string.IsNullOrWhiteSpace(query))
        {
            return false;
        }

        var collapsed = string
            .Join(' ', query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToLowerInvariant();

        if (collapsed.Length > maxLength)
        {
            return false;
        }

        normalized = Uri.EscapeDataString(collapsed);
        return true;
    }
}