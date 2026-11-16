using Microsoft.AspNetCore.Mvc;
using MovieSearchCase.Domain.Exceptions;
using MovieSearchCase.Domain.Interfaces.Services;
using MovieSearchCase.Shared;
using MovieSearchCase.WebApi.Mappers;

namespace MovieSearchCase.WebApi.Handlers.Movies;

public class GetTrailerVideoKeysHandler : IRequestHandlerAsync
{
    private readonly IMovieService _movieService;

    public GetTrailerVideoKeysHandler(IMovieService movieService)
    {
        _movieService = movieService;
    }

    public async Task<IActionResult> HandleAsync(HttpRequest request)
    {
        if (!request.RouteValues.TryGetValue("movieId", out var value)
        || !int.TryParse(value?.ToString(), out var movieId)
        || movieId < 1)
        {
            throw new MovieException(ExceptionTitleDetail.InvalidRequestTitle, ErrorType.InvalidRequest, "Invalid movie id.");
        }

        var result = await _movieService.GetTrailerVideoKeysAsync(movieId, request.HttpContext.RequestAborted);

        return new OkObjectResult(result.Select(movie => movie.ToApiModel()));
    }
}
