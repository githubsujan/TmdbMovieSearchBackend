using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MovieSearchCase.Domain.Interfaces.Clients;
using MovieSearchCase.Domain.Interfaces.Services;
using MovieSearchCase.Infrastructure.Caching;
using MovieSearchCase.Infrastructure.Clients;
using MovieSearchCase.Infrastructure.Services;
using MovieSearchCase.Shared.Extensions;

namespace MovieSearchCase.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddServices(this IServiceCollection services, IConfiguration configuration)
    {
        var cachingOptions = (configuration.GetSection(CachingOptions.SectionName).Get<CachingOptions>() ?? new CachingOptions()).ValidateOptions();

        services.AddScoped<MovieService>();
        services.AddScoped<TvService>();

        if (!cachingOptions.Enabled)
        {
            services.AddScoped<IMovieService>(sp => sp.GetRequiredService<MovieService>());
            services.AddScoped<ITvService>(sp => sp.GetRequiredService<TvService>());

            return services;
        }

        services.AddHybridCache(options =>
        {
            options.MaximumPayloadBytes = 256 * 1024;
            options.MaximumKeyLength = 512;
        });

        services.AddScoped<IMovieService>(sp => new CachedMovieService(
            sp.GetRequiredService<MovieService>(),
            sp.GetRequiredService<HybridCache>(),
            cachingOptions,
            sp.GetRequiredService<ILogger<CachedMovieService>>()));

        services.AddScoped<ITvService>(sp => new CachedTvService(
            sp.GetRequiredService<TvService>(),
            sp.GetRequiredService<HybridCache>(),
            cachingOptions,
            sp.GetRequiredService<ILogger<CachedTvService>>()));

        return services;
    }

    public static IServiceCollection AddClients(this IServiceCollection services, IConfiguration configuration)
    {
        var tmdbOptions = configuration.GetSection(nameof(TmdbOptions)).Get<TmdbOptions>().ValidateOptions();

        services.AddHttpClient<ITmdbClient, TmdbClient>(client =>
            {
                client.BaseAddress = new Uri(tmdbOptions.BaseUrl.TrimEnd('/') + "/");
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", tmdbOptions.AccessToken);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestVersion = HttpVersion.Version20;
                client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
                ConnectTimeout = TimeSpan.FromSeconds(5),
                EnableMultipleHttp2Connections = true,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
            })
            .SetHandlerLifetime(Timeout.InfiniteTimeSpan)
            .AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(15);
                options.Retry.MaxRetryAttempts = 2;
                options.Retry.Delay = TimeSpan.FromMilliseconds(300);
            });

        return services;
    }
}