using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using MovieSearchCase.WebApi.Application.Caching;
using MovieSearchCase.WebApi.Application.Versioning;
using MovieSearchCase.WebApi.Handlers.Movies;
using Swashbuckle.AspNetCore.Annotations;

namespace MovieSearchCase.WebApi.Controllers.V1;

[ApiVersion("1.0")]
[VersionedRoute]
public class MoviesController : ApiControllerBase
{
    private readonly IMoviesRequestHandlerFactory _requestHandlerFactory;

    public MoviesController(IMoviesRequestHandlerFactory requestHandlerFactory)
    {
        _requestHandlerFactory = requestHandlerFactory;
    }

    /// <summary>
    /// Returns this week's TMDB-backed trending movies.
    /// </summary>
    [HttpGet("trending")]
    [ClientCache(ClientCacheDuration.Details)]
    [SwaggerOperation(Summary = "Get trending movies", OperationId = "GetTrendingMovies")]
    [SwaggerResponse(StatusCodes.Status200OK, "This week's trending movies", typeof(IEnumerable<Models.Movies.Movie>))]
    public async Task<IActionResult> GetTrending() =>
        await _requestHandlerFactory.GetTrendingMovies().HandleAsync(Request);

    /// <summary>
    /// Returns searched movies.
    /// </summary>
    [HttpGet("search")]
    [ClientCache(ClientCacheDuration.Details)]
    [SwaggerOperation(Summary = "Search movies", OperationId = "SearchMovies")]
    [SwaggerResponse(StatusCodes.Status200OK, "Search movies", typeof(IEnumerable<Models.Movies.Movie>))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Invalid request parameters", typeof(ProblemDetails), "application/problem+json")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Not Found", typeof(ProblemDetails), "application/problem+json")]
    [SwaggerResponse(StatusCodes.Status503ServiceUnavailable, "Service Unavailable", typeof(ProblemDetails), "application/problem+json")]
    public async Task<IActionResult> Search([FromQuery] string query, [FromQuery] int page = 1) => 
        await _requestHandlerFactory.SearchMovies().HandleAsync(Request);

    /// <summary>
    /// Returns top level details of a movie by ID.
    /// </summary>
    [HttpGet("{movieId}")]
    [ClientCache(ClientCacheDuration.Details)]
    [SwaggerOperation(Summary = "Get movie details", OperationId = "GetMovieDetails")]
    [SwaggerResponse(StatusCodes.Status200OK, "Get movie details", typeof(Models.Movies.MovieExtendDetails))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Invalid request parameters", typeof(ProblemDetails), "application/problem+json")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Not Found", typeof(ProblemDetails), "application/problem+json")]
    [SwaggerResponse(StatusCodes.Status503ServiceUnavailable, "Service Unavailable", typeof(ProblemDetails), "application/problem+json")]
    public async Task<IActionResult> GetMovie([FromRoute] int movieId) =>
        await _requestHandlerFactory.GetMovie().HandleAsync(Request);

    /// <summary>
    /// Returns trailer video keys of a movie by ID.
    /// </summary>
    [HttpGet("{movieId}/videos")]
    [ClientCache(ClientCacheDuration.Details)]
    [SwaggerOperation(Summary = "Get trailer video keys of movie", OperationId = "GetTrailerVideoKeysOfMovie")]
    [SwaggerResponse(StatusCodes.Status200OK, "Get trailer video keys of movie", typeof(IEnumerable<Models.Movies.MovieTrailerVideoKeys>))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Invalid request parameters", typeof(ProblemDetails), "application/problem+json")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Not Found", typeof(ProblemDetails), "application/problem+json")]
    [SwaggerResponse(StatusCodes.Status503ServiceUnavailable, "Service Unavailable", typeof(ProblemDetails), "application/problem+json")]
    public async Task<IActionResult> GetTrailerVideoKeys([FromRoute] int movieId) =>
        await _requestHandlerFactory.GetTrailerVideoKeys().HandleAsync(Request);
}
