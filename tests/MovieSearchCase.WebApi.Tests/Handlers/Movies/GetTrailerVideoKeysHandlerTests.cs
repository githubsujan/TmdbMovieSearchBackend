using System.Collections;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using MovieSearchCase.Domain.Entities;
using MovieSearchCase.Domain.Exceptions;
using MovieSearchCase.Domain.Interfaces.Services;
using MovieSearchCase.WebApi.Handlers.Movies;
using Xunit;

namespace MovieSearchCase.WebApi.Tests.Handlers.Movies;

public class GetTrailerVideoKeysHandlerTests
{
    private const string InvalidMovieIdDetail = "Invalid movie id.";

    private readonly Mock<IMovieService> _movieServiceMock = new();
    private readonly GetTrailerVideoKeysHandler _handler;

    public GetTrailerVideoKeysHandlerTests()
    {
        _handler = new GetTrailerVideoKeysHandler(_movieServiceMock.Object);
    }

    private static MovieTrailerVideoKeys CreateVideo(string key = "dgC9Q0uhX70", string name = "Official Trailer") => new()
    {
        Key = key,
        Name = name,
        Site = "YouTube",
        Type = "Trailer",
    };

    private void SetupGetVideosReturns(List<MovieTrailerVideoKeys> videos) =>
        _movieServiceMock
            .Setup(service => service.GetTrailerVideoKeysAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(videos);

    private static HttpContext CreateHttpContext(object? movieId, bool includeRouteValue = true)
    {
        var httpContext = new DefaultHttpContext();
        if (includeRouteValue)
        {
            httpContext.Request.RouteValues["movieId"] = movieId;
        }

        return httpContext;
    }

    private void VerifyServiceNeverCalled() =>
        _movieServiceMock.Verify(
            service => service.GetTrailerVideoKeysAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);

    private static List<object> BodyItems(IActionResult result)
    {
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeAssignableTo<IEnumerable>();
        return ((IEnumerable)ok.Value!).Cast<object>().ToList();
    }

    // ---------- success ----------

    [Fact]
    public async Task HandleShouldReturnOkWithMappedVideosWhenIdIsValid()
    {
        SetupGetVideosReturns(
        [
            CreateVideo("dgC9Q0uhX70", "Official Trailer"),
            CreateVideo("abc123XYZ", "Teaser"),
        ]);

        var result = await _handler.HandleAsync(CreateHttpContext("268").Request);

        BodyItems(result).Should().BeEquivalentTo(
            new[]
            {
                new { Key = "dgC9Q0uhX70", Name = "Official Trailer", Site = "YouTube", Type = "Trailer" },
                new { Key = "abc123XYZ", Name = "Teaser", Site = "YouTube", Type = "Trailer" },
            },
            options => options.ExcludingMissingMembers().WithStrictOrdering());
    }

    [Fact]
    public async Task HandleShouldReturnOkWithEmptyListWhenMovieHasNoVideos()
    {
        SetupGetVideosReturns([]);

        var result = await _handler.HandleAsync(CreateHttpContext("268").Request);

        BodyItems(result).Should().BeEmpty();
    }

    [Fact]
    public async Task HandleShouldCallServiceWithParsedMovieId()
    {
        SetupGetVideosReturns([]);

        await _handler.HandleAsync(CreateHttpContext("268").Request);

        _movieServiceMock.Verify(
            service => service.GetTrailerVideoKeysAsync(268, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("2147483647")] // int.MaxValue
    public async Task HandleShouldAcceptBoundaryIds(string movieId)
    {
        SetupGetVideosReturns([]);

        var result = await _handler.HandleAsync(CreateHttpContext(movieId).Request);

        result.Should().BeOfType<OkObjectResult>();
        _movieServiceMock.Verify(
            service => service.GetTrailerVideoKeysAsync(int.Parse(movieId), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleShouldAcceptRouteValueThatIsAlreadyAnInteger()
    {
        SetupGetVideosReturns([]);

        var result = await _handler.HandleAsync(CreateHttpContext(268).Request);

        result.Should().BeOfType<OkObjectResult>();
        _movieServiceMock.Verify(
            service => service.GetTrailerVideoKeysAsync(268, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleShouldPassRequestAbortedTokenToService()
    {
        SetupGetVideosReturns([]);
        using var cts = new CancellationTokenSource();
        var httpContext = CreateHttpContext("268");
        httpContext.RequestAborted = cts.Token;

        await _handler.HandleAsync(httpContext.Request);

        _movieServiceMock.Verify(
            service => service.GetTrailerVideoKeysAsync(268, cts.Token),
            Times.Once);
    }

    // ---------- movieId validation ----------

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("-268")]
    [InlineData("abc")]
    [InlineData("1.5")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("99999999999")] // does not fit in an int
    [InlineData("268abc")]
    public async Task HandleShouldThrowInvalidRequestWhenMovieIdIsNotAPositiveInteger(string movieId)
    {
        Func<Task> act = () => _handler.HandleAsync(CreateHttpContext(movieId).Request);

        var exception = (await act.Should().ThrowAsync<MovieException>()).Which;
        exception.Title.Should().Be(ExceptionTitleDetail.InvalidRequestTitle);
        exception.ErrorCode.Should().Be(ErrorType.InvalidRequest);
        exception.ErrorCode.ToStatusCode().Should().Be(StatusCodes.Status400BadRequest);
        exception.Message.Should().Be(InvalidMovieIdDetail);

        VerifyServiceNeverCalled();
    }

    [Fact]
    public async Task HandleShouldThrowInvalidRequestWhenRouteValueIsMissing()
    {
        Func<Task> act = () => _handler.HandleAsync(CreateHttpContext(null, includeRouteValue: false).Request);

        var exception = (await act.Should().ThrowAsync<MovieException>()).Which;
        exception.ErrorCode.Should().Be(ErrorType.InvalidRequest);
        exception.Message.Should().Be(InvalidMovieIdDetail);

        VerifyServiceNeverCalled();
    }

    [Fact]
    public async Task HandleShouldThrowInvalidRequestWhenRouteValueIsNull()
    {
        Func<Task> act = () => _handler.HandleAsync(CreateHttpContext(null).Request);

        var exception = (await act.Should().ThrowAsync<MovieException>()).Which;
        exception.ErrorCode.Should().Be(ErrorType.InvalidRequest);
        exception.Message.Should().Be(InvalidMovieIdDetail);

        VerifyServiceNeverCalled();
    }

    // ---------- service failures ----------

    [Fact]
    public async Task HandleShouldPropagateNotFoundExceptionThrownByService()
    {
        var notFound = new MovieException(
            ExceptionTitleDetail.NotFoundTitle,
            ErrorType.EntityNotFound,
            ExceptionTitleDetail.NotFoundDetail);

        _movieServiceMock
            .Setup(service => service.GetTrailerVideoKeysAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(notFound);

        Func<Task> act = () => _handler.HandleAsync(CreateHttpContext("999999").Request);

        (await act.Should().ThrowAsync<MovieException>()).Which.Should().BeSameAs(notFound);
    }

    [Fact]
    public async Task HandleShouldPropagateUpstreamUnavailableExceptionThrownByService()
    {
        var unavailable = new MovieException(
            MovieException.ExceptionTitle,
            ErrorType.UpstreamServiceUnavailable,
            ExceptionTitleDetail.TimeoutDetail);

        _movieServiceMock
            .Setup(service => service.GetTrailerVideoKeysAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(unavailable);

        Func<Task> act = () => _handler.HandleAsync(CreateHttpContext("268").Request);

        (await act.Should().ThrowAsync<MovieException>()).Which.Should().BeSameAs(unavailable);
    }
}