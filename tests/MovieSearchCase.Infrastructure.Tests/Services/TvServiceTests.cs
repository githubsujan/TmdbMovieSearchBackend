using FluentAssertions;
using Moq;
using MovieSearchCase.Domain.Entities;
using MovieSearchCase.Domain.Exceptions;
using MovieSearchCase.Domain.Interfaces.Clients;
using MovieSearchCase.Infrastructure.Services;
using Xunit;

namespace MovieSearchCase.Infrastructure.Tests.Services;

public class TvServiceTests
{
    private readonly Mock<ITmdbClient> _tmdbClientMock = new();
    private readonly TvService _tvService;

    public TvServiceTests()
    {
        _tvService = new TvService(_tmdbClientMock.Object);
    }

    private void SetupSearchReturns(TvSearchResponse<Tv> response) =>
        _tmdbClientMock
            .Setup(client => client.SearchTvAsync(It.IsAny<TvSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

    // ---------- results ----------

    [Fact]
    public async Task SearchTvAsyncShouldReturnResponseFromTmdbClient()
    {
        var request = new TvSearchRequest("breaking", 2, 2008);
        var expected = new TvSearchResponse<Tv>
        {
            Page = 2,
            TotalPages = 12,
            TotalResults = 231,
            Results = [It.IsAny<Tv>(), It.IsAny<Tv>()],
        };

        _tmdbClientMock
            .Setup(client => client.SearchTvAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _tvService.SearchTvAsync(request, CancellationToken.None);

        result.Should().BeSameAs(expected);
        result.Page.Should().Be(2);
        result.TotalPages.Should().Be(12);
        result.TotalResults.Should().Be(231);
        result.Results.Should().HaveCount(2);
    }

    [Fact]
    public async Task SearchTvAsyncShouldReturnEmptyResponseWhenTmdbHasNoMatches()
    {
        SetupSearchReturns(new TvSearchResponse<Tv>
        {
            Page = 1,
            TotalPages = 0,
            TotalResults = 0,
            Results = [],
        });

        var result = await _tvService.SearchTvAsync(new TvSearchRequest("zzzzzz", 1, null), CancellationToken.None);

        result.TotalPages.Should().Be(0);
        result.TotalResults.Should().Be(0);
        result.Results.Should().BeEmpty();
    }

    // ---------- arguments are forwarded unchanged ----------

    [Fact]
    public async Task SearchTvAsyncShouldPassRequestAndCancellationTokenToTmdbClientUnchanged()
    {
        using var cts = new CancellationTokenSource();
        var request = new TvSearchRequest("breaking", 3, 2008);
        SetupSearchReturns(new TvSearchResponse<Tv> { Results = [] });

        await _tvService.SearchTvAsync(request, cts.Token);

        _tmdbClientMock.Verify(client => client.SearchTvAsync(request, cts.Token), Times.Once);
        _tmdbClientMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SearchTvAsyncShouldForwardRequestWithoutFirstAirDateYear()
    {
        var request = new TvSearchRequest("breaking", 1, null);
        SetupSearchReturns(new TvSearchResponse<Tv> { Results = [] });

        await _tvService.SearchTvAsync(request, CancellationToken.None);

        _tmdbClientMock.Verify(
            client => client.SearchTvAsync(
                It.Is<TvSearchRequest>(r => r.query == "breaking" && r.page == 1 && r.firstAirDateYear == null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ---------- failures ----------

    [Fact]
    public async Task SearchTvAsyncShouldPropagateTvExceptionFromTmdbClient()
    {
        var expected = new TvException(
            "Tv request failed",
            ErrorType.UpstreamServiceUnavailable,
            ExceptionTitleDetail.TimeoutDetail);

        _tmdbClientMock
            .Setup(client => client.SearchTvAsync(It.IsAny<TvSearchRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expected);

        Func<Task> act = () => _tvService.SearchTvAsync(new TvSearchRequest("breaking", 1, null), CancellationToken.None);

        (await act.Should().ThrowAsync<TvException>()).Which.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task SearchTvAsyncShouldPropagateNotFoundExceptionFromTmdbClient()
    {
        var expected = new TvException(
            ExceptionTitleDetail.NotFoundTitle,
            ErrorType.EntityNotFound,
            ExceptionTitleDetail.NotFoundDetail);

        _tmdbClientMock
            .Setup(client => client.SearchTvAsync(It.IsAny<TvSearchRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expected);

        Func<Task> act = () => _tvService.SearchTvAsync(new TvSearchRequest("breaking", 1, null), CancellationToken.None);

        (await act.Should().ThrowAsync<TvException>()).Which.ErrorCode.Should().Be(ErrorType.EntityNotFound);
    }

    [Fact]
    public async Task SearchTvAsyncShouldNotSwallowCancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _tmdbClientMock
            .Setup(client => client.SearchTvAsync(It.IsAny<TvSearchRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        Func<Task> act = () => _tvService.SearchTvAsync(new TvSearchRequest("breaking", 1, null), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}