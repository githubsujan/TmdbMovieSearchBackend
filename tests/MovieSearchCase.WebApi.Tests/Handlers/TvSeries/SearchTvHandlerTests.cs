using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using MovieSearchCase.Domain.Entities;
using MovieSearchCase.Domain.Exceptions;
using MovieSearchCase.Domain.Interfaces.Services;
using MovieSearchCase.WebApi.Handlers.TvSeries;
using Xunit;
using ApiTv = MovieSearchCase.WebApi.Models.TvSeries.Tv;

namespace MovieSearchCase.WebApi.Tests.Handlers.TvSeries;

public class SearchTvHandlerTests
{
    private const string QueryRequiredDetail = "The 'query' parameter is required.";
    private const string PageRangeDetail = "The 'page' parameter must be between 1 and 500.";
    private const string YearRangeDetail = "The 'firstAirDateYear' parameter must be between 1000 and 9999.";

    private readonly Mock<ITvService> _tvServiceMock = new();
    private readonly SearchTvHandler _handler;

    public SearchTvHandlerTests()
    {
        _handler = new SearchTvHandler(_tvServiceMock.Object);
    }

    private static Tv CreateTv(int id = 1396, string name = "Breaking Bad") => new()
    {
        Id = id,
        Name = name,
    };

    private static TvSearchResponse<Tv> EmptyResponse() => new()
    {
        Page = 1,
        TotalPages = 0,
        TotalResults = 0,
        Results = [],
    };

    private void SetupSearchReturns(TvSearchResponse<Tv> response) =>
        _tvServiceMock
            .Setup(service => service.SearchTvAsync(It.IsAny<TvSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

    private static HttpContext CreateHttpContext(string queryString)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString(queryString);
        return httpContext;
    }

    private void VerifyServiceNeverCalled() =>
        _tvServiceMock.Verify(
            service => service.SearchTvAsync(It.IsAny<TvSearchRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);

    private void VerifyServiceCalledWith(string query, int page, int? firstAirDateYear) =>
        _tvServiceMock.Verify(
            service => service.SearchTvAsync(
                It.Is<TvSearchRequest>(r => r.query == query && r.page == page && r.firstAirDateYear == firstAirDateYear),
                It.IsAny<CancellationToken>()),
            Times.Once);

    private static async Task<TvException> ShouldThrowTvException(Func<Task> act) =>
        (await act.Should().ThrowAsync<TvException>()).Which;

    // ---------- success ----------

    [Fact]
    public async Task HandleShouldReturnOkWithMappedPagedResponseWhenServiceSucceeds()
    {
        SetupSearchReturns(new TvSearchResponse<Tv>
        {
            Page = 2,
            TotalPages = 12,
            TotalResults = 231,
            Results = [CreateTv(1396, "Breaking Bad"), CreateTv(1399, "Game of Thrones")],
        });

        var result = await _handler.HandleAsync(CreateHttpContext("?query=breaking&page=2").Request);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeOfType<TvSearchResponse<ApiTv>>().Subject;
        body.Page.Should().Be(2);
        body.TotalPages.Should().Be(12);
        body.TotalResults.Should().Be(231);
        body.Results.Should().BeEquivalentTo(
            new[]
            {
                new { Id = 1396, Name = "Breaking Bad" },
                new { Id = 1399, Name = "Game of Thrones" },
            },
            options => options.ExcludingMissingMembers().WithStrictOrdering());
    }

    [Fact]
    public async Task HandleShouldReturnOkWithEmptyResultsWhenNothingMatches()
    {
        SetupSearchReturns(EmptyResponse());

        var result = await _handler.HandleAsync(CreateHttpContext("?query=zzzzzz").Request);

        var body = result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<TvSearchResponse<ApiTv>>().Subject;
        body.Page.Should().Be(1);
        body.TotalPages.Should().Be(0);
        body.TotalResults.Should().Be(0);
        body.Results.Should().BeEmpty();
    }

    // ---------- defaults, trimming, pass-through ----------

    [Fact]
    public async Task HandleShouldDefaultPageToOneAndYearToNullWhenNotProvided()
    {
        SetupSearchReturns(EmptyResponse());

        await _handler.HandleAsync(CreateHttpContext("?query=breaking").Request);

        VerifyServiceCalledWith("breaking", 1, null);
    }

    [Fact]
    public async Task HandleShouldTrimSurroundingWhitespaceFromQuery()
    {
        SetupSearchReturns(EmptyResponse());

        await _handler.HandleAsync(CreateHttpContext("?query=%20%20breaking%20bad%20%20&page=3").Request);

        VerifyServiceCalledWith("breaking bad", 3, null);
    }

    [Fact]
    public async Task HandleShouldPassFirstAirDateYearToServiceWhenProvided()
    {
        SetupSearchReturns(EmptyResponse());

        await _handler.HandleAsync(CreateHttpContext("?query=breaking&page=2&firstAirDateYear=2008").Request);

        VerifyServiceCalledWith("breaking", 2, 2008);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(500)]
    public async Task HandleShouldAcceptPageBoundaryValues(int page)
    {
        SetupSearchReturns(EmptyResponse());

        var result = await _handler.HandleAsync(CreateHttpContext($"?query=breaking&page={page}").Request);

        result.Should().BeOfType<OkObjectResult>();
        VerifyServiceCalledWith("breaking", page, null);
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(2008)]
    [InlineData(9999)]
    public async Task HandleShouldAcceptFirstAirDateYearBoundaryValues(int year)
    {
        SetupSearchReturns(EmptyResponse());

        var result = await _handler.HandleAsync(CreateHttpContext($"?query=breaking&firstAirDateYear={year}").Request);

        result.Should().BeOfType<OkObjectResult>();
        VerifyServiceCalledWith("breaking", 1, year);
    }

    [Fact]
    public async Task HandleShouldPassRequestAbortedTokenToService()
    {
        SetupSearchReturns(EmptyResponse());
        using var cts = new CancellationTokenSource();
        var httpContext = CreateHttpContext("?query=breaking");
        httpContext.RequestAborted = cts.Token;

        await _handler.HandleAsync(httpContext.Request);

        _tvServiceMock.Verify(
            service => service.SearchTvAsync(It.IsAny<TvSearchRequest>(), cts.Token),
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

        var exception = await ShouldThrowTvException(act);
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
    [InlineData("99999999999")] // does not fit in an int
    public async Task HandleShouldThrowInvalidRequestWhenPageIsOutOfRangeOrNotANumber(string page)
    {
        Func<Task> act = () => _handler.HandleAsync(CreateHttpContext($"?query=breaking&page={page}").Request);

        var exception = await ShouldThrowTvException(act);
        exception.Title.Should().Be(ExceptionTitleDetail.InvalidRequestTitle);
        exception.ErrorCode.Should().Be(ErrorType.InvalidRequest);
        exception.Message.Should().Be(PageRangeDetail);

        VerifyServiceNeverCalled();
    }

    // ---------- firstAirDateYear validation ----------

    [Theory]
    [InlineData("0")]
    [InlineData("999")]
    [InlineData("10000")]
    [InlineData("-2008")]
    [InlineData("abc")]
    [InlineData("2008.5")]
    [InlineData("")]
    [InlineData("99999999999")] // does not fit in an int
    public async Task HandleShouldThrowInvalidRequestWhenFirstAirDateYearIsOutOfRangeOrNotANumber(string year)
    {
        Func<Task> act = () => _handler.HandleAsync(CreateHttpContext($"?query=breaking&firstAirDateYear={year}").Request);

        var exception = await ShouldThrowTvException(act);
        exception.Title.Should().Be(ExceptionTitleDetail.InvalidRequestTitle);
        exception.ErrorCode.Should().Be(ErrorType.InvalidRequest);
        exception.Message.Should().Be(YearRangeDetail);

        VerifyServiceNeverCalled();
    }

    // ---------- validation order ----------

    [Fact]
    public async Task HandleShouldValidateQueryBeforePageAndYear()
    {
        Func<Task> act = () => _handler.HandleAsync(CreateHttpContext("?page=abc&firstAirDateYear=abc").Request);

        var exception = await ShouldThrowTvException(act);
        exception.Message.Should().Be(QueryRequiredDetail);
    }

    [Fact]
    public async Task HandleShouldValidatePageBeforeYear()
    {
        Func<Task> act = () => _handler.HandleAsync(CreateHttpContext("?query=breaking&page=0&firstAirDateYear=abc").Request);

        var exception = await ShouldThrowTvException(act);
        exception.Message.Should().Be(PageRangeDetail);
    }

    // ---------- service failures ----------

    [Fact]
    public async Task HandleShouldPropagateTvExceptionThrownByService()
    {
        var serviceException = new TvException(
            "Tv request failed",
            ErrorType.UpstreamServiceUnavailable,
            ExceptionTitleDetail.TimeoutDetail);

        _tvServiceMock
            .Setup(service => service.SearchTvAsync(It.IsAny<TvSearchRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(serviceException);

        Func<Task> act = () => _handler.HandleAsync(CreateHttpContext("?query=breaking").Request);

        (await act.Should().ThrowAsync<TvException>()).Which.Should().BeSameAs(serviceException);
    }
}