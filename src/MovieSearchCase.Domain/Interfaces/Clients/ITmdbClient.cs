using MovieSearchCase.Domain.Entities;

namespace MovieSearchCase.Domain.Interfaces.Clients;

/// <summary>
/// Talks to The Movie Database (TMDB) REST API. See https://developer.themoviedb.org/reference.
/// </summary>
public interface ITmdbClient
{
    /// <summary>
    /// Returns this week's trending movies (TMDB's /trending/movie/week endpoint).
    /// </summary>
    Task<IReadOnlyList<Movie>> GetTrendingMoviesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Returns searched movies (TMDB's /search/movie endpoint).
    /// </summary>
    Task<MovieSearchResponse<Movie>> SearchMoviesAsync(MovieSearchRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Returns top level details of a movie by ID (TMDB's /movie/movie_id endpoint).
    /// </summary>
    Task<MovieExtendDetails> GetMovieAsync(int movieId, CancellationToken cancellationToken);

    /// <summary>
    /// Returns trailer video keys of a movie by ID (TMDB's /movie/movie_id/videos endpoint).
    /// </summary>
    Task<IReadOnlyList<MovieTrailerVideoKeys>> GetTrailerVideoKeysAsync(int movieId, CancellationToken cancellationToken);

    /// <summary>
    /// Returns searched tv/series (TMDB's /search/tv endpoint).
    /// </summary>
    Task<TvSearchResponse<Tv>> SearchTvAsync(TvSearchRequest request, CancellationToken cancellationToken);
}
