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

public class GetMovieHandlerTests
{
    private const string InvalidMovieIdDetail = "Invalid movie id.";

    private readonly Mock<IMovieService> _movieServiceMock = new();
    private readonly GetMovieHandler _handler;

    public GetMovieHandlerTests()
    {
        _handler = new GetMovieHandler(_movieServiceMock.Object);
    }

    private static MovieExtendDetails CreateMovie(int id = 268) => new()
    {
        Id = id,
        Title = "Batman",
    };

    private void SetupGetMovieReturns(MovieExtendDetails movie) =>
        _movieServiceMock
            .Setup(service => service.GetMovieAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(movie);

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
            service => service.GetMovieAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);

    // ---------- success ----------

    [Fact]
    public async Task HandleShouldReturnOkWithMappedMovieWhenIdIsValid()
    {
        SetupGetMovieReturns(CreateMovie(268));

        var result = await _handler.HandleAsync(CreateHttpContext("268").Request);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().NotBeNull();
        ok.Value.Should().BeEquivalentTo(
            new { Id = 268, Title = "Batman" },
            options => options.ExcludingMissingMembers());
    }

    [Fact]
    public async Task HandleShouldCallServiceWithParsedMovieId()
    {
        SetupGetMovieReturns(CreateMovie(268));

        await _handler.HandleAsync(CreateHttpContext("268").Request);

        _movieServiceMock.Verify(
            service => service.GetMovieAsync(268, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("2147483647")] // int.MaxValue
    public async Task HandleShouldAcceptBoundaryIds(string movieId)
    {
        SetupGetMovieReturns(CreateMovie(int.Parse(movieId)));

        var result = await _handler.HandleAsync(CreateHttpContext(movieId).Request);

        result.Should().BeOfType<OkObjectResult>();
        _movieServiceMock.Verify(
            service => service.GetMovieAsync(int.Parse(movieId), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleShouldAcceptRouteValueThatIsAlreadyAnInteger()
    {
        SetupGetMovieReturns(CreateMovie(268));

        var result = await _handler.HandleAsync(CreateHttpContext(268).Request);

        result.Should().BeOfType<OkObjectResult>();
        _movieServiceMock.Verify(
            service => service.GetMovieAsync(268, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleShouldPassRequestAbortedTokenToService()
    {
        SetupGetMovieReturns(CreateMovie());
        using var cts = new CancellationTokenSource();
        var httpContext = CreateHttpContext("268");
        httpContext.RequestAborted = cts.Token;

        await _handler.HandleAsync(httpContext.Request);

        _movieServiceMock.Verify(
            service => service.GetMovieAsync(268, cts.Token),
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
            .Setup(service => service.GetMovieAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
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
            .Setup(service => service.GetMovieAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(unavailable);

        Func<Task> act = () => _handler.HandleAsync(CreateHttpContext("268").Request);

        (await act.Should().ThrowAsync<MovieException>()).Which.Should().BeSameAs(unavailable);
    }
}