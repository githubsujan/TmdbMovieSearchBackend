using MovieSearchCase.Domain.Entities;

namespace MovieSearchCase.Domain.Interfaces.Services;

/// <summary>
/// TMDB-backed movie lookups used by the API.
/// </summary>
public interface IMovieService
{
    Task<IReadOnlyList<Movie>> GetTrendingAsync(CancellationToken cancellationToken);

    Task<MovieSearchResponse<Movie>> SearchMoviesAsync(MovieSearchRequest request, CancellationToken cancellationToken);

    Task<MovieExtendDetails> GetMovieAsync(int movieId, CancellationToken cancellationToken);

    Task<IReadOnlyList<MovieTrailerVideoKeys>> GetTrailerVideoKeysAsync(int movieId, CancellationToken cancellationToken);
}
