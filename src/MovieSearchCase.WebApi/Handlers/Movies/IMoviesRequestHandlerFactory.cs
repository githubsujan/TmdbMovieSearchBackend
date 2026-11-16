using MovieSearchCase.Shared;

namespace MovieSearchCase.WebApi.Handlers.Movies;

public interface IMoviesRequestHandlerFactory
{
    IRequestHandlerAsync GetTrendingMovies();

    IRequestHandlerAsync SearchMovies();

    IRequestHandlerAsync GetMovie();
    IRequestHandlerAsync GetTrailerVideoKeys();
}
