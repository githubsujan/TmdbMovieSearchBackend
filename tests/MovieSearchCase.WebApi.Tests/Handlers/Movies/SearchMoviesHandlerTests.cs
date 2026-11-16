using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using MovieSearchCase.Domain.Entities;
using MovieSearchCase.Domain.Exceptions;
using MovieSearchCase.Domain.Interfaces.Services;
using MovieSearchCase.WebApi.Handlers.Movies;
using Xunit;
using ApiMovie = MovieSearchCase.WebApi.Models.Movies.Movie;

namespace MovieSearchCase.WebApi.Tests.Handlers.Movies;

public class SearchMoviesHandlerTests
{
    private const string QueryRequiredDetail = "The 'query' parameter is required.";
    private const string PageRangeDetail = "The 'page' parameter must be between 1 and 500.";

    private readonly Mock<IMovieService> _movieServiceMock = new();
    private readonly SearchMoviesHandler _handler;

    public SearchMoviesHandlerTests()
    {
        _handler = new SearchMoviesHandler(_movieServiceMock.Object);
    }

    private static HttpContext CreateHttpContext(string queryString)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString(queryString);
        return httpContext;
    }

    private void SetupSearchReturns(MovieSearchResponse<Movie> response) =>
        _movieServiceMock
            .Setup(service => service.SearchMoviesAsync(It.IsAny<MovieSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

    private static MovieSearchResponse<Movie> EmptyResponse() => new()
    {
        Page = 1,
        TotalPages = 0,
        TotalResults = 0,
        Results = [],
    };

    // ---------- success ----------

    [Fact]
    public async Task HandleShouldReturnOkWithMappedPagedResponseWhenServiceSucceeds()
    {
        SetupSearchReturns(new MovieSearchResponse<Movie>
        {
            Page = 2,
            TotalPages = 12,
            TotalResults = 231,
            Results =
            [
                new Movie
                {
                    Id = 268,
                    Title = "Batman",
                    Overview = "Batman must face his most ruthless nemesis…",
                    PosterPath = "/poster.jpg",
                    BackdropPath = "/backdrop.jpg",
                    VoteAverage = 7.2,
                    ReleaseDate = new DateOnly(1989, 6, 21),
                },
            ],
        });

        var result = await _handler.HandleAsync(CreateHttpContext("?query=batman&page=2").Request);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeOfType<MovieSearchResponse<ApiMovie>>().Subject;
        body.Page.Should().Be(2);
        body.TotalPages.Should().Be(12);
        body.TotalResults.Should().Be(231);
        body.Results.Should().ContainSingle().Which.Should().BeEquivalentTo(new ApiMovie
        {
            Id = 268,
            Title = "Batman",
            Overview = "Batman must face his most ruthless nemesis…",
            PosterPath = "/poster.jpg",
            BackdropPath = "/backdrop.jpg",
            VoteAverage = 7.2,
            ReleaseDate = new DateOnly(1989, 6, 21),
        });
    }

    [Fact]
    public async Task HandleShouldReturnOkWithEmptyResultsWhenNothingMatches()
    {
        SetupSearchReturns(EmptyResponse());

        var result = await _handler.HandleAsync(CreateHttpContext("?query=zzzzzz").Request);

        var body = result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<MovieSearchResponse<ApiMovie>>().Subject;
        body.TotalPages.Should().Be(0);
        body.TotalResults.Should().Be(0);
        body.Results.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleShouldDefaultPageToOneWhenPageIsNotProvided()
    {
        SetupSearchReturns(EmptyResponse());

        await _handler.HandleAsync(CreateHttpContext("?query=batman").Request);

        _movieServiceMock.Verify(
            service => service.SearchMoviesAsync(new MovieSearchRequest("batman", 1), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleShouldTrimSurroundingWhitespaceFromQuery()
    {
        SetupSearchReturns(EmptyResponse());

        await _handler.HandleAsync(CreateHttpContext("?query=%20%20batman%20%20&page=3").Request);

        _movieServiceMock.Verify(
            service => service.SearchMoviesAsync(new MovieSearchRequest("batman", 3), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(500)]
    public async Task HandleShouldAcceptPageBoundaryValues(int page)
    {
        SetupSearchReturns(EmptyResponse());

        var result = await _handler.HandleAsync(CreateHttpContext($"?query=batman&page={page}").Request);

        result.Should().BeOfType<OkObjectResult>();
        _movieServiceMock.Verify(
            service => service.SearchMoviesAsync(new MovieSearchRequest("batman", page), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleShouldPassRequestAbortedTokenToService()
    {
        SetupSearchReturns(EmptyResponse());
        using var cts = new CancellationTokenSource();
        var httpContext = CreateHttpContext("?query=batman");
        httpContext.RequestAborted = cts.Token;

        await _handler.HandleAsync(httpContext.Request);

        _movieServiceMock.Verify(
            service => service.SearchMoviesAsync(It.IsAny<MovieSearchRequest>(), cts.Token),
            Times.Once);
    }

    // ---------- query validation ----------

    [Theory]
    [InlineData("")]                    // no query string at all
    [InlineData("?page=2")]             // query parameter missing
    [InlineData("?query=")]             // query parameter empty
    [InlineData("?query=%20%20%20")]    // query parameter only spaces
    public async Task HandleShouldThrowInvalidRequestWhenQueryIsMissingOrBlank(string queryString)
    {
        Func<Task> act = () => _handler.HandleAsync(CreateHttpContext(queryString).Request);

        var exception = (await act.Should().ThrowAsync<MovieException>()).Subject.Single();
        exception.Title.Should().Be(ExceptionTitleDetail.InvalidRequestTitle);
        exception.ErrorCode.Should().Be(ErrorType.InvalidRequest);
        exception.ErrorCode.ToStatusCode().Should().Be(StatusCodes.Status400BadRequest);
        exception.Message.Should().Be(QueryRequiredDetail);

        VerifyServiceNeverCalled();
    }

    // ---------- page validation ----------

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("501")]
    [InlineData("abc")]
    [InlineData("1.5")]
    [InlineData("")]
    public async Task HandleShouldThrowInvalidRequestWhenPageIsOutOfRangeOrNotANumber(string page)
    {
        Func<Task> act = () => _handler.HandleAsync(CreateHttpContext($"?query=batman&page={page}").Request);

        var exception = (await act.Should().ThrowAsync<MovieException>()).Subject.Single();
        exception.Title.Should().Be(ExceptionTitleDetail.InvalidRequestTitle);
        exception.ErrorCode.Should().Be(ErrorType.InvalidRequest);
        exception.Message.Should().Be(PageRangeDetail);

        VerifyServiceNeverCalled();
    }

    [Fact]
    public async Task HandleShouldValidateQueryBeforePage()
    {
        Func<Task> act = () => _handler.HandleAsync(CreateHttpContext("?page=abc").Request);

        var exception = (await act.Should().ThrowAsync<MovieException>()).Subject.Single();
        exception.Message.Should().Be(QueryRequiredDetail);
    }

    // ---------- service failures ----------

    [Fact]
    public async Task HandleShouldPropagateMovieExceptionThrownByService()
    {
        var serviceException = new MovieException(
            MovieException.ExceptionTitle,
            ErrorType.UpstreamServiceUnavailable,
            ExceptionTitleDetail.TimeoutDetail);

        _movieServiceMock
            .Setup(service => service.SearchMoviesAsync(It.IsAny<MovieSearchRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(serviceException);

        Func<Task> act = () => _handler.HandleAsync(CreateHttpContext("?query=batman").Request);

        (await act.Should().ThrowAsync<MovieException>()).Which.Should().BeSameAs(serviceException);
    }

    private void VerifyServiceNeverCalled() =>
        _movieServiceMock.Verify(
            service => service.SearchMoviesAsync(It.IsAny<MovieSearchRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
}