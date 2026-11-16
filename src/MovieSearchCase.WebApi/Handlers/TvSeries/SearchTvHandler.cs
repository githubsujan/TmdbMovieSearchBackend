using Microsoft.AspNetCore.Mvc;
using MovieSearchCase.Domain.Entities;
using MovieSearchCase.Domain.Exceptions;
using MovieSearchCase.Domain.Interfaces.Services;
using MovieSearchCase.Shared;
using MovieSearchCase.WebApi.Mappers;

namespace MovieSearchCase.WebApi.Handlers.TvSeries;

public class SearchTvHandler : IRequestHandlerAsync
{
    private readonly ITvService _tvService;

    public SearchTvHandler(ITvService tvService)
    {
        _tvService = tvService;
    }

    public async Task<IActionResult> HandleAsync(HttpRequest request)
    {
        var query = request.Query["query"].ToString().Trim();

        if (string.IsNullOrWhiteSpace(query))
            throw new TvException(ExceptionTitleDetail.InvalidRequestTitle, ErrorType.InvalidRequest, "The 'query' parameter is required.");

        var page = 1;
        if (request.Query.ContainsKey("page") &&
            (!int.TryParse(request.Query["page"], out page) || page is < 1 or > 500))
            throw new TvException(ExceptionTitleDetail.InvalidRequestTitle, ErrorType.InvalidRequest, "The 'page' parameter must be between 1 and 500.");

        int? firstAirDateYear = null;
        if (request.Query.ContainsKey("firstAirDateYear"))
        {
            if (!int.TryParse(request.Query["firstAirDateYear"], out var year) || year is < 1000 or > 9999)
                throw new TvException(ExceptionTitleDetail.InvalidRequestTitle, ErrorType.InvalidRequest, "The 'firstAirDateYear' parameter must be between 1000 and 9999.");
            firstAirDateYear = year;
        }

        var response = await _tvService.SearchTvAsync(new TvSearchRequest(query,page, firstAirDateYear), request.HttpContext.RequestAborted);

        return new OkObjectResult(new TvSearchResponse<Models.TvSeries.Tv>
        {
            Page = response.Page,
            Results = response.Results.Select(tv => tv.ToApiModel()).ToList(),
            TotalPages = response.TotalPages,
            TotalResults = response.TotalResults
        });
    }
}
