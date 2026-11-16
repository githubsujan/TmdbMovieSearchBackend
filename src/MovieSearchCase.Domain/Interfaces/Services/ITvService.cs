using MovieSearchCase.Domain.Entities;

namespace MovieSearchCase.Domain.Interfaces.Services;

/// <summary>
/// TMDB-backed Tv/Series lookups used by the API.
/// </summary>
public interface ITvService
{
    Task<TvSearchResponse<Tv>> SearchTvAsync(TvSearchRequest request, CancellationToken cancellationToken);
}
