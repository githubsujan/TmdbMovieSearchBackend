using Microsoft.AspNetCore.Mvc;
using MovieSearchCase.Domain.Entities;
using MovieSearchCase.Domain.Exceptions;
using MovieSearchCase.Domain.Interfaces.Services;
using MovieSearchCase.Shared;
using MovieSearchCase.WebApi.Mappers;

namespace MovieSearchCase.WebApi.Handlers.Movies;

public class SearchMoviesHandler : IRequestHandlerAsync
{
    private readonly IMovieService _movieService;

    public SearchMoviesHandler(IMovieService movieService)
    {
        _movieService = movieService;
    }

    public async Task<IActionResult> HandleAsync(HttpRequest request)
    {
        var query = request.Query["query"].ToString().Trim();

        if (string.IsNullOrWhiteSpace(query))
            throw new MovieException(ExceptionTitleDetail.InvalidRequestTitle, ErrorType.InvalidRequest, "The 'query' parameter is required.");

        var page = 1;
        if (request.Query.ContainsKey("page") &&
            (!int.TryParse(request.Query["page"], out page) || page is < 1 or > 500))
            throw new MovieException(ExceptionTitleDetail.InvalidRequestTitle, ErrorType.InvalidRequest, "The 'page' parameter must be between 1 and 500.");

        var response = await _movieService.SearchMoviesAsync(new MovieSearchRequest(query,page), request.HttpContext.RequestAborted);

        return new OkObjectResult(new MovieSearchResponse<Models.Movies.Movie>
        {
            Page = response.Page,
            Results = response.Results.Select(movie => movie.ToApiModel()).ToList(),
            TotalPages = response.TotalPages,
            TotalResults = response.TotalResults
        });
    }
}
