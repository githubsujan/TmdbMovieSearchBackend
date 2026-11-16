using MovieSearchCase.Shared;

namespace MovieSearchCase.WebApi.Handlers.TvSeries;

public interface ITvRequestHandlerFactory
{
    IRequestHandlerAsync SearchTv();
}
