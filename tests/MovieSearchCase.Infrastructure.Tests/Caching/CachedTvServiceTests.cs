using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MovieSearchCase.Domain.Entities;
using MovieSearchCase.Domain.Exceptions;
using MovieSearchCase.Domain.Interfaces.Services;
using MovieSearchCase.Infrastructure.Caching;
using Xunit;

namespace MovieSearchCase.Infrastructure.Tests.Caching;

public sealed class CachedTvServiceTests : IDisposable
{
    private readonly HybridCacheHost _host = new();
    private readonly Mock<ITvService> _inner = new();

    public void Dispose() => _host.Dispose();

    private CachedTvService CreateSut(
        CachingOptions? options = null,
        ILogger<CachedTvService>? logger = null) =>
        new(_inner.Object, _host.Cache, options ?? new CachingOptions(), logger ?? NullLogger<CachedTvService>.Instance);

    private static TvSearchResponse<Tv> TvResult(string name = "Breaking Bad") => new()
    {
        Page = 1,
        TotalPages = 4,
        TotalResults = 71,
        Results =
        [
            new Tv
            {
                Id = 1396,
                Name = name,
                Overview = "A teacher turns to crime.",
                VoteAverage = 8.9,
                FirstAirDate = new DateOnly(2008, 1, 20)
            }
        ]
    };

    private void SetupSearch(TvSearchResponse<Tv>? response = null) =>
        _inner.Setup(s => s.SearchTvAsync(It.IsAny<TvSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response ?? TvResult());

    private void VerifySearchCalls(Times times) =>
        _inner.Verify(s => s.SearchTvAsync(It.IsAny<TvSearchRequest>(), It.IsAny<CancellationToken>()), times);

    // ---------- Hit / miss ----------

    [Fact]
    public async Task SearchTvAsync_OnMiss_CallsInnerWithOriginalRequestAndReturnsItsResult()
    {
        var request = new TvSearchRequest("Breaking Bad", 2, 2008);
        SetupSearch();
        var sut = CreateSut();

        var result = await sut.SearchTvAsync(request, CancellationToken.None);

        result.Should().BeEquivalentTo(TvResult());
        _inner.Verify(s => s.SearchTvAsync(request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchTvAsync_OnHit_DoesNotCallInnerAgainAndReturnsSameData()
    {
        SetupSearch();
        var sut = CreateSut();
        var request = new TvSearchRequest("breaking bad", 1, null);

        var first = await sut.SearchTvAsync(request, CancellationToken.None);
        var second = await sut.SearchTvAsync(request, CancellationToken.None);

        second.Should().BeEquivalentTo(first);
        second.Results.Single().FirstAirDate.Should().Be(new DateOnly(2008, 1, 20));
        VerifySearchCalls(Times.Once());
    }

    [Fact]
    public async Task SearchTvAsync_EmptyResultIsCachedToo()
    {
        SetupSearch(new TvSearchResponse<Tv> { Page = 1, Results = [] });
        var sut = CreateSut();
        var request = new TvSearchRequest("zzzzzz", 1, null);

        var first = await sut.SearchTvAsync(request, CancellationToken.None);
        var second = await sut.SearchTvAsync(request, CancellationToken.None);

        first.Results.Should().BeEmpty();
        second.Results.Should().BeEmpty();
        VerifySearchCalls(Times.Once());
    }

    // ---------- Key composition ----------

    [Theory]
    [InlineData("Breaking Bad")]
    [InlineData("BREAKING BAD")]
    [InlineData("  breaking bad  ")]
    [InlineData("breaking    bad")]
    [InlineData("\tBreaking \n Bad ")]
    public async Task SearchTvAsync_QueriesDifferingOnlyByCaseOrWhitespace_ShareOneCacheEntry(string variant)
    {
        SetupSearch();
        var sut = CreateSut();

        await sut.SearchTvAsync(new TvSearchRequest("breaking bad", 1, null), CancellationToken.None);
        await sut.SearchTvAsync(new TvSearchRequest(variant, 1, null), CancellationToken.None);

        VerifySearchCalls(Times.Once());
    }

    [Fact]
    public async Task SearchTvAsync_DifferentPages_AreCachedSeparately()
    {
        SetupSearch();
        var sut = CreateSut();

        await sut.SearchTvAsync(new TvSearchRequest("breaking bad", 1, null), CancellationToken.None);
        await sut.SearchTvAsync(new TvSearchRequest("breaking bad", 2, null), CancellationToken.None);
        await sut.SearchTvAsync(new TvSearchRequest("breaking bad", 1, null), CancellationToken.None);
        await sut.SearchTvAsync(new TvSearchRequest("breaking bad", 2, null), CancellationToken.None);

        VerifySearchCalls(Times.Exactly(2));
    }

    [Fact]
    public async Task SearchTvAsync_DifferentQueries_AreCachedSeparately()
    {
        SetupSearch();
        var sut = CreateSut();

        await sut.SearchTvAsync(new TvSearchRequest("breaking bad", 1, null), CancellationToken.None);
        await sut.SearchTvAsync(new TvSearchRequest("better call saul", 1, null), CancellationToken.None);

        VerifySearchCalls(Times.Exactly(2));
    }

    [Fact]
    public async Task SearchTvAsync_DifferentFirstAirDateYears_AreCachedSeparately()
    {
        SetupSearch();
        var sut = CreateSut();

        await sut.SearchTvAsync(new TvSearchRequest("doctor who", 1, 1963), CancellationToken.None);
        await sut.SearchTvAsync(new TvSearchRequest("doctor who", 1, 2005), CancellationToken.None);
        await sut.SearchTvAsync(new TvSearchRequest("doctor who", 1, 1963), CancellationToken.None);
        await sut.SearchTvAsync(new TvSearchRequest("doctor who", 1, 2005), CancellationToken.None);

        VerifySearchCalls(Times.Exactly(2));
    }

    [Fact]
    public async Task SearchTvAsync_NoYearAndSpecificYear_AreCachedSeparately()
    {
        SetupSearch();
        var sut = CreateSut();

        await sut.SearchTvAsync(new TvSearchRequest("doctor who", 1, null), CancellationToken.None);
        await sut.SearchTvAsync(new TvSearchRequest("doctor who", 1, 2005), CancellationToken.None);
        await sut.SearchTvAsync(new TvSearchRequest("doctor who", 1, null), CancellationToken.None);

        VerifySearchCalls(Times.Exactly(2));
    }

    [Fact]
    public async Task SearchTvAsync_ReturnsTheCachedResponseMatchingEachRequest()
    {
        _inner.Setup(s => s.SearchTvAsync(It.Is<TvSearchRequest>(r => r.query == "a"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TvResult("Show A"));
        _inner.Setup(s => s.SearchTvAsync(It.Is<TvSearchRequest>(r => r.query == "b"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TvResult("Show B"));
        var sut = CreateSut();

        await sut.SearchTvAsync(new TvSearchRequest("a", 1, null), CancellationToken.None);
        await sut.SearchTvAsync(new TvSearchRequest("b", 1, null), CancellationToken.None);
        var a = await sut.SearchTvAsync(new TvSearchRequest("a", 1, null), CancellationToken.None);
        var b = await sut.SearchTvAsync(new TvSearchRequest("b", 1, null), CancellationToken.None);

        a.Results.Single().Name.Should().Be("Show A");
        b.Results.Single().Name.Should().Be("Show B");
    }

    // ---------- Bypass ----------

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   \t  ")]
    public async Task SearchTvAsync_WhenQueryBlank_BypassesCacheAndCallsInnerEveryTime(string query)
    {
        var response = TvResult();
        SetupSearch(response);
        var sut = CreateSut();
        var request = new TvSearchRequest(query, 1, null);

        var first = await sut.SearchTvAsync(request, CancellationToken.None);
        var second = await sut.SearchTvAsync(request, CancellationToken.None);

        first.Should().BeSameAs(response);
        second.Should().BeSameAs(response);
        _inner.Verify(s => s.SearchTvAsync(request, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task SearchTvAsync_WhenQueryLongerThanMaxLength_BypassesCache()
    {
        SetupSearch();
        var sut = CreateSut(new CachingOptions { MaxQueryLength = 10 });
        var request = new TvSearchRequest(new string('a', 11), 1, null);

        await sut.SearchTvAsync(request, CancellationToken.None);
        await sut.SearchTvAsync(request, CancellationToken.None);

        VerifySearchCalls(Times.Exactly(2));
    }

    [Fact]
    public async Task SearchTvAsync_WhenQueryExactlyMaxLength_IsCached()
    {
        SetupSearch();
        var sut = CreateSut(new CachingOptions { MaxQueryLength = 10 });
        var request = new TvSearchRequest(new string('a', 10), 1, null);

        await sut.SearchTvAsync(request, CancellationToken.None);
        await sut.SearchTvAsync(request, CancellationToken.None);

        VerifySearchCalls(Times.Once());
    }

    [Fact]
    public async Task SearchTvAsync_MaxLengthIsMeasuredAfterTrimmingAndCollapsingWhitespace()
    {
        SetupSearch();
        var sut = CreateSut(new CachingOptions { MaxQueryLength = 5 });
        var request = new TvSearchRequest("   a   b   c   ", 1, null);

        await sut.SearchTvAsync(request, CancellationToken.None);
        await sut.SearchTvAsync(request, CancellationToken.None);

        VerifySearchCalls(Times.Once());
    }

    // ---------- Failures ----------

    [Fact]
    public async Task SearchTvAsync_WhenInnerThrows_PropagatesAndDoesNotCacheTheFailure()
    {
        var request = new TvSearchRequest("breaking bad", 1, null);
        _inner.SetupSequence(s => s.SearchTvAsync(It.IsAny<TvSearchRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new MovieException(
                "Tv request failed", ErrorType.UpstreamServiceUnavailable, "TMDB did not return the searched tv/series."))
            .ReturnsAsync(TvResult());
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<MovieException>(() => sut.SearchTvAsync(request, CancellationToken.None));
        var recovered = await sut.SearchTvAsync(request, CancellationToken.None);

        ex.ErrorCode.Should().Be(ErrorType.UpstreamServiceUnavailable);
        ex.Title.Should().Be("Tv request failed");
        recovered.Should().BeEquivalentTo(TvResult());
        VerifySearchCalls(Times.Exactly(2));
    }

    [Fact]
    public async Task SearchTvAsync_WhenBypassingCacheAndInnerThrows_PropagatesSameException()
    {
        var exception = new InvalidOperationException("boom");
        _inner.Setup(s => s.SearchTvAsync(It.IsAny<TvSearchRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);
        var sut = CreateSut();

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.SearchTvAsync(new TvSearchRequest("  ", 1, null), CancellationToken.None));

        Assert.Same(exception, thrown);
    }

    [Fact]
    public async Task SearchTvAsync_WhenInnerIsCancelled_PropagatesCancellationAndDoesNotCacheIt()
    {
        using var cts = new CancellationTokenSource();
        var request = new TvSearchRequest("breaking bad", 1, null);
        _inner.SetupSequence(s => s.SearchTvAsync(It.IsAny<TvSearchRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token))
            .ReturnsAsync(TvResult());
        var sut = CreateSut();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.SearchTvAsync(request, cts.Token));
        var recovered = await sut.SearchTvAsync(request, CancellationToken.None);

        recovered.Should().BeEquivalentTo(TvResult());
        VerifySearchCalls(Times.Exactly(2));
    }

    [Fact]
    public async Task SearchTvAsync_ConcurrentIdenticalRequests_CallInnerOnlyOnce()
    {
        var gate = new TaskCompletionSource();
        _inner.Setup(s => s.SearchTvAsync(It.IsAny<TvSearchRequest>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await gate.Task;
                return TvResult();
            });
        var sut = CreateSut();

        var calls = Enumerable.Range(0, 10)
            .Select(_ => sut.SearchTvAsync(new TvSearchRequest("breaking bad", 1, null), CancellationToken.None))
            .ToArray();
        gate.SetResult();
        var results = await Task.WhenAll(calls);

        results.Should().AllSatisfy(r => r.Should().BeEquivalentTo(TvResult()));
        VerifySearchCalls(Times.Once());
    }

    // ---------- Logging / isolation ----------

    [Theory]
    [InlineData(null, "v1:tv:search:breaking%20bad:2:any")]
    [InlineData(2008, "v1:tv:search:breaking%20bad:2:2008")]
    public async Task SearchTvAsync_LogsCacheMissWithTvSearchKey(int? year, string expectedKey)
    {
        SetupSearch();
        var logger = new Mock<ILogger<CachedTvService>>();
        var sut = CreateSut(logger: logger.Object);

        await sut.SearchTvAsync(new TvSearchRequest("  Breaking   Bad ", 2, year), CancellationToken.None);
        await sut.SearchTvAsync(new TvSearchRequest("breaking bad", 2, year), CancellationToken.None);

        logger.Verify(l => l.Log(
            LogLevel.Debug,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains(expectedKey + ";")),
            null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public async Task TvAndMovieSearchesWithSameQueryAndPage_DoNotShareCacheEntries()
    {
        var movieInner = new Mock<IMovieService>();
        movieInner.Setup(s => s.SearchMoviesAsync(It.IsAny<MovieSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MovieSearchResponse<Movie>
            {
                Page = 1,
                Results = [new Movie { Id = 1, Title = "A movie" }]
            });
        SetupSearch(TvResult("A show"));
        var movies = new CachedMovieService(movieInner.Object, _host.Cache, new CachingOptions(), NullLogger<CachedMovieService>.Instance);
        var tv = CreateSut();

        var movieResult = await movies.SearchMoviesAsync(new MovieSearchRequest("the office", 1), CancellationToken.None);
        var tvResult = await tv.SearchTvAsync(new TvSearchRequest("the office", 1, null), CancellationToken.None);

        movieResult.Results.Single().Title.Should().Be("A movie");
        tvResult.Results.Single().Name.Should().Be("A show");
        movieInner.Verify(s => s.SearchMoviesAsync(It.IsAny<MovieSearchRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        VerifySearchCalls(Times.Once());
    }

    [Fact]
    public async Task CachedEntriesAreSharedAcrossServiceInstancesUsingTheSameCache()
    {
        SetupSearch();
        var request = new TvSearchRequest("breaking bad", 1, null);

        await CreateSut().SearchTvAsync(request, CancellationToken.None);
        await CreateSut().SearchTvAsync(request, CancellationToken.None);

        VerifySearchCalls(Times.Once());
    }
}
