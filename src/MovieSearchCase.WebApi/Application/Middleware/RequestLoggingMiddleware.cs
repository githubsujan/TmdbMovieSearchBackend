using Microsoft.AspNetCore.Mvc.Infrastructure;
using MovieSearchCase.Domain.Exceptions;

namespace MovieSearchCase.WebApi.Application.Middleware;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext httpContext, ProblemDetailsFactory problemDetailsFactory)
    {
        try
        {
            await _next(httpContext);
        }
        catch (Exception exception)
        {
            var (statusCode, title, detail, instance) = exception switch
            {
                ProblemDetailsException problemDetailsException => (
                    problemDetailsException.ErrorCode.ToStatusCode(),
                    problemDetailsException.Title,
                    problemDetailsException.Message,
                    problemDetailsException.Instance.ToString()),
                HttpRequestException => (
                    StatusCodes.Status503ServiceUnavailable,
                    ExceptionTitleDetail.ServiceUnavailableTitle,
                    exception.Message,
                    httpContext.Request.Path.ToString()),
                _ => (
                    StatusCodes.Status500InternalServerError,
                    ExceptionTitleDetail.ServerErrorTitle,
                    exception.Message,
                    httpContext.Request.Path.ToString()),
            };

            _logger.LogError(exception, "Request to {Path} failed with status {StatusCode}", httpContext.Request.Path, statusCode);

            var problemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Type = instance,
                Title = title,
                Status = statusCode,
                Detail = detail,
            };

            httpContext.Response.StatusCode = statusCode;
            await httpContext.Response.WriteAsJsonAsync(problemDetails, options: null, contentType: "application/problem+json");
        }
    }
}

public static class RequestLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app) =>
        app.UseMiddleware<RequestLoggingMiddleware>();
}
