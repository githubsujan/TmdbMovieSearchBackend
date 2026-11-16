using MovieSearchCase.WebApi.Handlers.Movies;
using MovieSearchCase.WebApi.Handlers.TvSeries;

namespace MovieSearchCase.WebApi.Handlers;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRequestHandlerFactories(this IServiceCollection services)
    {
        services.AddScoped<IMoviesRequestHandlerFactory, MoviesRequestHandlerFactory>();
        services.AddScoped<ITvRequestHandlerFactory, TvRequestHandlerFactory>();
        return services;
    }
}
