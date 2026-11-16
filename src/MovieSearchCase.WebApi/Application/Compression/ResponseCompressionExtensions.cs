using System.IO.Compression;
using Microsoft.AspNetCore.ResponseCompression;

namespace MovieSearchCase.WebApi.Application.Compression;

public static class ResponseCompressionExtensions
{
    public static IServiceCollection AddCustomResponseCompression(this IServiceCollection services)
    {
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
        });

        services.Configure<BrotliCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
        services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);

        return services;
    }

    public static IApplicationBuilder UseCustomResponseCompression(this IApplicationBuilder app) =>
        app.UseResponseCompression();
}