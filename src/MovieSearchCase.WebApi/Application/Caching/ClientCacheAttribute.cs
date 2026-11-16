using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MovieSearchCase.WebApi.Application.Caching;

public static class ClientCacheDuration
{
    public const int Search = 300;
    public const int Trending = 900;
    public const int Details = 3600;
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class ClientCacheAttribute : ResultFilterAttribute
{
    private readonly string _cacheControl;

    public ClientCacheAttribute(int maxAgeSeconds)
    {
        _cacheControl = $"public, max-age={maxAgeSeconds}";
    }

    public override void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is ObjectResult { StatusCode: null or StatusCodes.Status200OK })
        {
            context.HttpContext.Response.Headers.CacheControl = _cacheControl;
        }
    }
}