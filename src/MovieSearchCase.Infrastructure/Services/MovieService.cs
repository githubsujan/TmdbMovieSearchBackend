using MovieSearchCase.Domain.Entities;
using MovieSearchCase.Domain.Interfaces.Clients;
using MovieSearchCase.Domain.Interfaces.Services;

namespace MovieSearchCase.Infrastructure.Services;

public class MovieService : IMovieService
{
    private readonly ITmdbClient _tmdbClient;

    public MovieService(ITmdbClient tmdbClient)
    {
        _tmdbClient = tmdbClient;
    }

    public Task<IReadOnlyList<Movie>> GetTrendingAsync(CancellationToken cancellationToken) =>
        _tmdbClient.GetTrendingMoviesAsync(cancellationToken);

    public Task<MovieSearchResponse<Movie>> SearchMoviesAsync(MovieSearchRequest request, CancellationToken cancellationToken) =>
        _tmdbClient.SearchMoviesAsync(request, cancellationToken);

    public Task<MovieExtendDetails> GetMovieAsync(int movieId, CancellationToken cancellationToken) => 
        _tmdbClient.GetMovieAsync(movieId, cancellationToken);

    public Task<IReadOnlyList<MovieTrailerVideoKeys>> GetTrailerVideoKeysAsync(int movieId, CancellationToken cancellationToken) => 
        _tmdbClient.GetTrailerVideoKeysAsync(movieId, cancellationToken);
}
