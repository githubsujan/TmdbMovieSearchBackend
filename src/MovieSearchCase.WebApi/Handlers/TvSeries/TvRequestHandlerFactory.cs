using MovieSearchCase.Domain.Interfaces.Services;
using MovieSearchCase.Shared;

namespace MovieSearchCase.WebApi.Handlers.TvSeries;

public class TvRequestHandlerFactory : ITvRequestHandlerFactory
{
    private readonly ITvService _tvService;

    public TvRequestHandlerFactory(ITvService tvService)
    {
        _tvService = tvService;
    }

    public IRequestHandlerAsync SearchTv() => new SearchTvHandler(_tvService);
}
