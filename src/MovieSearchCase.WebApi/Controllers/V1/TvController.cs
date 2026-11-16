using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using MovieSearchCase.WebApi.Application.Caching;
using MovieSearchCase.WebApi.Application.Versioning;
using MovieSearchCase.WebApi.Handlers.TvSeries;
using Swashbuckle.AspNetCore.Annotations;

namespace MovieSearchCase.WebApi.Controllers.V1;

[ApiVersion("1.0")]
[VersionedRoute]
public class TvController : ApiControllerBase
{
    private readonly ITvRequestHandlerFactory _requestHandlerFactory;

    public TvController(ITvRequestHandlerFactory requestHandlerFactory)
    {
        _requestHandlerFactory = requestHandlerFactory;
    }

    /// <summary>
    /// Returns searched tv/series.
    /// </summary>
    [HttpGet("search")]
    [ClientCache(ClientCacheDuration.Details)]
    [SwaggerOperation(Summary = "Search tv/series", OperationId = "SearchTv")]
    [SwaggerResponse(StatusCodes.Status200OK, "Search tv/series", typeof(IEnumerable<Models.TvSeries.Tv>))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Invalid request parameters", typeof(ProblemDetails), "application/problem+json")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Not Found", typeof(ProblemDetails), "application/problem+json")]
    [SwaggerResponse(StatusCodes.Status503ServiceUnavailable, "Service Unavailable", typeof(ProblemDetails), "application/problem+json")]
    public async Task<IActionResult> Search([FromQuery, Required(ErrorMessage = "Tv name cannot be empty or only space.")] string query,
        [FromQuery, Range(1, 500)] int page = 1, [FromQuery, Range(1000, 9999)] int? firstAirDateYear = null)
    {
        return await _requestHandlerFactory.SearchTv().HandleAsync(Request);
    }
}
