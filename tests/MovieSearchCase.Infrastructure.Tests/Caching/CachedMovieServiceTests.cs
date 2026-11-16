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

public sealed class CachedMovieServiceTests : IDisposable
{
    private readonly HybridCacheHost _host = new();
    private readonly Mock<IMovieService> _inner = new();

    public void Dispose() => _host.Dispose();

    private CachedMovieService CreateSut(
        CachingOptions? options = null,
        ILogger<CachedMovieService>? logger = null) =>
        new(_inner.Object, _host.Cache, options ?? new CachingOptions(), logger ?? NullLogger<CachedMovieService>.Instance);

    private static IReadOnlyList<Movie> Trending(params int[] ids) =>
        ids.Select(id => new Movie { Id = id, Title = $"Movie {id}", VoteAverage = 7.5 }).ToList();

    private static MovieSearchResponse<Movie> SearchResult(string title = "Inception") => new()
    {
        Page = 1,
        TotalPages = 3,
        TotalResults = 42,
        Results = [new Movie { Id = 27205, Title = title, ReleaseDate = new DateOnly(2010, 7, 15) }]
    };

    private static MovieExtendDetails Details(int id) => new()
    {
        Id = id,
        Title = $"Movie {id}",
        Runtime = 136,
        Tagline = "tagline",
        ReleaseDate = new DateOnly(1999, 3, 31),
        Genres = [new Genre(28, "Action")]
    };

    private static IReadOnlyList<MovieTrailerVideoKeys> Trailers(string key = "abc123") =>
        [new MovieTrailerVideoKeys { Name = "Official Trailer", Key = key, Site = "YouTube", Type = "Trailer" }];

    private void SetupSearch(MovieSearchResponse<Movie>? response = null) =>
        _inner.Setup(s => s.SearchMoviesAsync(It.IsAny<MovieSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response ?? SearchResult());

    private void VerifySearchCalls(Times times) =>
        _inner.Verify(s => s.SearchMoviesAsync(It.IsAny<MovieSearchRequest>(), It.IsAny<CancellationToken>()), times);

    // =====================================================
    // GetTrendingAsync
    // =====================================================

    [Fact]
    public async Task GetTrendingAsync_OnMiss_CallsInnerAndReturnsItsResult()
    {
        _inner.Setup(s => s.GetTrendingAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Trending(1, 2));
        var sut = CreateSut();

        var result = await sut.GetTrendingAsync(CancellationToken.None);

        result.Should().BeEquivalentTo(Trending(1, 2));
        _inner.Verify(s => s.GetTrendingAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTrendingAsync_OnHit_DoesNotCallInnerAgainAndReturnsSameData()
    {
        _inner.Setup(s => s.GetTrendingAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Trending(1, 2));
        var sut = CreateSut();

        var first = await sut.GetTrendingAsync(CancellationToken.None);
        var second = await sut.GetTrendingAsync(CancellationToken.None);
        var third = await sut.GetTrendingAsync(CancellationToken.None);

        second.Should().BeEquivalentTo(first);
        third.Should().BeEquivalentTo(first);
        _inner.Verify(s => s.GetTrendingAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTrendingAsync_EmptyResultIsCachedToo()
    {
        _inner.Setup(s => s.GetTrendingAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Trending());
        var sut = CreateSut();

        var first = await sut.GetTrendingAsync(CancellationToken.None);
        var second = await sut.GetTrendingAsync(CancellationToken.None);

        first.Should().BeEmpty();
        second.Should().BeEmpty();
        _inner.Verify(s => s.GetTrendingAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTrendingAsync_WhenInnerThrows_PropagatesAndDoesNotCacheTheFailure()
    {
        _inner.SetupSequence(s => s.GetTrendingAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new MovieException(
                MovieException.ExceptionTitle, ErrorType.UpstreamServiceUnavailable, "TMDB did not return trending movies."))
            .ReturnsAsync(Trending(5));
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<MovieException>(() => sut.GetTrendingAsync(CancellationToken.None));
        var recovered = await sut.GetTrendingAsync(CancellationToken.None);

        ex.ErrorCode.Should().Be(ErrorType.UpstreamServiceUnavailable);
        ex.Details.Should().Be("TMDB did not return trending movies.");
        recovered.Should().BeEquivalentTo(Trending(5));
        _inner.Verify(s => s.GetTrendingAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GetTrendingAsync_WhenInnerIsCancelled_PropagatesCancellationAndDoesNotCacheIt()
    {
        using var cts = new CancellationTokenSource();
        _inner.SetupSequence(s => s.GetTrendingAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token))
            .ReturnsAsync(Trending(9));
        var sut = CreateSut();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.GetTrendingAsync(cts.Token));
        var recovered = await sut.GetTrendingAsync(CancellationToken.None);

        recovered.Should().BeEquivalentTo(Trending(9));
        _inner.Verify(s => s.GetTrendingAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GetTrendingAsync_ConcurrentCallsOnColdCache_CallInnerOnlyOnce()
    {
        var gate = new TaskCompletionSource();
        _inner.Setup(s => s.GetTrendingAsync(It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await gate.Task;
                return Trending(1);
            });
        var sut = CreateSut();

        var calls = Enumerable.Range(0, 10).Select(_ => sut.GetTrendingAsync(CancellationToken.None)).ToArray();
        gate.SetResult();
        var results = await Task.WhenAll(calls);

        results.Should().AllSatisfy(r => r.Should().BeEquivalentTo(Trending(1)));
        _inner.Verify(s => s.GetTrendingAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTrendingAsync_LogsCacheMissOnlyOnce()
    {
        _inner.Setup(s => s.GetTrendingAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Trending(1));
        var logger = new Mock<ILogger<CachedMovieService>>();
        var sut = CreateSut(logger: logger.Object);

        await sut.GetTrendingAsync(CancellationToken.None);
        await sut.GetTrendingAsync(CancellationToken.None);

        logger.Verify(l => l.Log(
            LogLevel.Debug,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("v1:movie:trending")),
            null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    // =====================================================
    // SearchMoviesAsync
    // =====================================================

    [Fact]
    public async Task SearchMoviesAsync_OnMiss_CallsInnerWithOriginalRequestAndReturnsItsResult()
    {
        var request = new MovieSearchRequest("Inception", 2);
        SetupSearch();
        var sut = CreateSut();

        var result = await sut.SearchMoviesAsync(request, CancellationToken.None);

        result.Should().BeEquivalentTo(SearchResult());
        _inner.Verify(s => s.SearchMoviesAsync(request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchMoviesAsync_OnHit_DoesNotCallInnerAgain()
    {
        SetupSearch();
        var sut = CreateSut();
        var request = new MovieSearchRequest("inception", 1);

        var first = await sut.SearchMoviesAsync(request, CancellationToken.None);
        var second = await sut.SearchMoviesAsync(request, CancellationToken.None);

        second.Should().BeEquivalentTo(first);
        VerifySearchCalls(Times.Once());
    }

    [Theory]
    [InlineData("Inception")]
    [InlineData("INCEPTION")]
    [InlineData("  inception  ")]
    [InlineData("\tInception\n")]
    public async Task SearchMoviesAsync_QueriesDifferingOnlyByCaseOrOuterWhitespace_ShareOneCacheEntry(string variant)
    {
        SetupSearch();
        var sut = CreateSut();

        await sut.SearchMoviesAsync(new MovieSearchRequest("inception", 1), CancellationToken.None);
        await sut.SearchMoviesAsync(new MovieSearchRequest(variant, 1), CancellationToken.None);

        VerifySearchCalls(Times.Once());
    }

    [Fact]
    public async Task SearchMoviesAsync_InnerWhitespaceIsCollapsedForTheCacheKey()
    {
        SetupSearch();
        var sut = CreateSut();

        await sut.SearchMoviesAsync(new MovieSearchRequest("the dark knight", 1), CancellationToken.None);
        await sut.SearchMoviesAsync(new MovieSearchRequest("the   dark \t knight", 1), CancellationToken.None);

        VerifySearchCalls(Times.Once());
    }

    [Fact]
    public async Task SearchMoviesAsync_DifferentPages_AreCachedSeparately()
    {
        SetupSearch();
        var sut = CreateSut();

        await sut.SearchMoviesAsync(new MovieSearchRequest("inception", 1), CancellationToken.None);
        await sut.SearchMoviesAsync(new MovieSearchRequest("inception", 2), CancellationToken.None);
        await sut.SearchMoviesAsync(new MovieSearchRequest("inception", 1), CancellationToken.None);
        await sut.SearchMoviesAsync(new MovieSearchRequest("inception", 2), CancellationToken.None);

        VerifySearchCalls(Times.Exactly(2));
    }

    [Fact]
    public async Task SearchMoviesAsync_DifferentQueries_AreCachedSeparately()
    {
        SetupSearch();
        var sut = CreateSut();

        await sut.SearchMoviesAsync(new MovieSearchRequest("inception", 1), CancellationToken.None);
        await sut.SearchMoviesAsync(new MovieSearchRequest("interstellar", 1), CancellationToken.None);

        VerifySearchCalls(Times.Exactly(2));
    }

    [Fact]
    public async Task SearchMoviesAsync_QueriesWithSpecialCharacters_DoNotCollide()
    {
        SetupSearch();
        var sut = CreateSut();

        await sut.SearchMoviesAsync(new MovieSearchRequest("a:b", 1), CancellationToken.None);
        await sut.SearchMoviesAsync(new MovieSearchRequest("a b", 1), CancellationToken.None);
        await sut.SearchMoviesAsync(new MovieSearchRequest("a&b", 1), CancellationToken.None);

        VerifySearchCalls(Times.Exactly(3));
    }

    [Fact]
    public async Task SearchMoviesAsync_ReturnsTheCachedResponseMatchingEachRequest()
    {
        _inner.Setup(s => s.SearchMoviesAsync(It.Is<MovieSearchRequest>(r => r.query == "a"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult("Result A"));
        _inner.Setup(s => s.SearchMoviesAsync(It.Is<MovieSearchRequest>(r => r.query == "b"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult("Result B"));
        var sut = CreateSut();

        await sut.SearchMoviesAsync(new MovieSearchRequest("a", 1), CancellationToken.None);
        await sut.SearchMoviesAsync(new MovieSearchRequest("b", 1), CancellationToken.None);
        var a = await sut.SearchMoviesAsync(new MovieSearchRequest("a", 1), CancellationToken.None);
        var b = await sut.SearchMoviesAsync(new MovieSearchRequest("b", 1), CancellationToken.None);

        a.Results.Single().Title.Should().Be("Result A");
        b.Results.Single().Title.Should().Be("Result B");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   \t  ")]
    public async Task SearchMoviesAsync_WhenQueryBlank_BypassesCacheAndCallsInnerEveryTime(string query)
    {
        var response = SearchResult();
        SetupSearch(response);
        var sut = CreateSut();
        var request = new MovieSearchRequest(query, 1);

        var first = await sut.SearchMoviesAsync(request, CancellationToken.None);
        var second = await sut.SearchMoviesAsync(request, CancellationToken.None);

        first.Should().BeSameAs(response);
        second.Should().BeSameAs(response);
        _inner.Verify(s => s.SearchMoviesAsync(request, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task SearchMoviesAsync_WhenQueryLongerThanMaxLength_BypassesCache()
    {
        SetupSearch();
        var sut = CreateSut(new CachingOptions { MaxQueryLength = 10 });
        var request = new MovieSearchRequest(new string('a', 11), 1);

        await sut.SearchMoviesAsync(request, CancellationToken.None);
        await sut.SearchMoviesAsync(request, CancellationToken.None);

        VerifySearchCalls(Times.Exactly(2));
    }

    [Fact]
    public async Task SearchMoviesAsync_WhenQueryExactlyMaxLength_IsCached()
    {
        SetupSearch();
        var sut = CreateSut(new CachingOptions { MaxQueryLength = 10 });
        var request = new MovieSearchRequest(new string('a', 10), 1);

        await sut.SearchMoviesAsync(request, CancellationToken.None);
        await sut.SearchMoviesAsync(request, CancellationToken.None);

        VerifySearchCalls(Times.Once());
    }

    [Fact]
    public async Task SearchMoviesAsync_MaxLengthIsMeasuredAfterTrimmingAndCollapsingWhitespace()
    {
        SetupSearch();
        var sut = CreateSut(new CachingOptions { MaxQueryLength = 5 });
        var request = new MovieSearchRequest("   a   b   c   ", 1);

        await sut.SearchMoviesAsync(request, CancellationToken.None);
        await sut.SearchMoviesAsync(request, CancellationToken.None);

        VerifySearchCalls(Times.Once());
    }

    [Fact]
    public async Task SearchMoviesAsync_WhenInnerThrows_PropagatesAndDoesNotCacheTheFailure()
    {
        var request = new MovieSearchRequest("inception", 1);
        _inner.SetupSequence(s => s.SearchMoviesAsync(It.IsAny<MovieSearchRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new MovieException(
                MovieException.ExceptionTitle, ErrorType.UpstreamServiceUnavailable, "TMDB did not return the searched movie."))
            .ReturnsAsync(SearchResult());
        var sut = CreateSut();

        await Assert.ThrowsAsync<MovieException>(() => sut.SearchMoviesAsync(request, CancellationToken.None));
        var recovered = await sut.SearchMoviesAsync(request, CancellationToken.None);

        recovered.Should().BeEquivalentTo(SearchResult());
        VerifySearchCalls(Times.Exactly(2));
    }

    [Fact]
    public async Task SearchMoviesAsync_WhenBypassingCacheAndInnerThrows_PropagatesSameException()
    {
        var exception = new InvalidOperationException("boom");
        _inner.Setup(s => s.SearchMoviesAsync(It.IsAny<MovieSearchRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);
        var sut = CreateSut();

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.SearchMoviesAsync(new MovieSearchRequest("  ", 1), CancellationToken.None));

        Assert.Same(exception, thrown);
    }

    [Fact]
    public async Task SearchMoviesAsync_ConcurrentIdenticalRequests_CallInnerOnlyOnce()
    {
        var gate = new TaskCompletionSource();
        _inner.Setup(s => s.SearchMoviesAsync(It.IsAny<MovieSearchRequest>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await gate.Task;
                return SearchResult();
            });
        var sut = CreateSut();

        var calls = Enumerable.Range(0, 10)
            .Select(_ => sut.SearchMoviesAsync(new MovieSearchRequest("inception", 1), CancellationToken.None))
            .ToArray();
        gate.SetResult();
        await Task.WhenAll(calls);

        VerifySearchCalls(Times.Once());
    }

    [Fact]
    public async Task SearchMoviesAsync_LogsCacheMissWithSearchKey()
    {
        SetupSearch();
        var logger = new Mock<ILogger<CachedMovieService>>();
        var sut = CreateSut(logger: logger.Object);

        await sut.SearchMoviesAsync(new MovieSearchRequest("  Fast   Furious ", 3), CancellationToken.None);

        logger.Verify(l => l.Log(
            LogLevel.Debug,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("v1:movie:search:fast%20furious:3")),
            null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    // =====================================================
    // GetMovieAsync (details)
    // =====================================================

    [Fact]
    public async Task GetMovieAsync_OnMiss_CallsInnerWithIdAndReturnsItsResult()
    {
        _inner.Setup(s => s.GetMovieAsync(603, It.IsAny<CancellationToken>())).ReturnsAsync(Details(603));
        var sut = CreateSut();

        var result = await sut.GetMovieAsync(603, CancellationToken.None);

        result.Should().BeEquivalentTo(Details(603));
        _inner.Verify(s => s.GetMovieAsync(603, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMovieAsync_OnHit_DoesNotCallInnerAgain()
    {
        _inner.Setup(s => s.GetMovieAsync(603, It.IsAny<CancellationToken>())).ReturnsAsync(Details(603));
        var sut = CreateSut();

        var first = await sut.GetMovieAsync(603, CancellationToken.None);
        var second = await sut.GetMovieAsync(603, CancellationToken.None);

        second.Should().BeEquivalentTo(first);
        second.Genres.Should().ContainSingle(g => g.id == 28 && g.name == "Action");
        _inner.Verify(s => s.GetMovieAsync(603, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMovieAsync_DifferentIds_AreCachedSeparately()
    {
        _inner.Setup(s => s.GetMovieAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) => Details(id));
        var sut = CreateSut();

        var a = await sut.GetMovieAsync(1, CancellationToken.None);
        var b = await sut.GetMovieAsync(2, CancellationToken.None);
        var a2 = await sut.GetMovieAsync(1, CancellationToken.None);
        var b2 = await sut.GetMovieAsync(2, CancellationToken.None);

        a.Id.Should().Be(1);
        b.Id.Should().Be(2);
        a2.Id.Should().Be(1);
        b2.Id.Should().Be(2);
        _inner.Verify(s => s.GetMovieAsync(1, It.IsAny<CancellationToken>()), Times.Once);
        _inner.Verify(s => s.GetMovieAsync(2, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMovieAsync_WhenMovieNotFound_PropagatesAndDoesNotCacheTheFailure()
    {
        _inner.SetupSequence(s => s.GetMovieAsync(999, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new MovieException("Invalid Request", ErrorType.EntityNotFound, "Movie with id 999 was not found."))
            .ReturnsAsync(Details(999));
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<MovieException>(() => sut.GetMovieAsync(999, CancellationToken.None));
        var recovered = await sut.GetMovieAsync(999, CancellationToken.None);

        ex.ErrorCode.Should().Be(ErrorType.EntityNotFound);
        ex.Details.Should().Be("Movie with id 999 was not found.");
        recovered.Id.Should().Be(999);
        _inner.Verify(s => s.GetMovieAsync(999, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GetMovieAsync_WhenUpstreamUnavailable_PropagatesException()
    {
        _inner.Setup(s => s.GetMovieAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new MovieException(
                MovieException.ExceptionTitle, ErrorType.UpstreamServiceUnavailable, "TMDB request timed out."));
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<MovieException>(() => sut.GetMovieAsync(1, CancellationToken.None));

        ex.ErrorCode.Should().Be(ErrorType.UpstreamServiceUnavailable);
        ex.Details.Should().Be("TMDB request timed out.");
    }

    [Fact]
    public async Task GetMovieAsync_ConcurrentCallsForSameId_CallInnerOnlyOnce()
    {
        var gate = new TaskCompletionSource();
        _inner.Setup(s => s.GetMovieAsync(603, It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await gate.Task;
                return Details(603);
            });
        var sut = CreateSut();

        var calls = Enumerable.Range(0, 10).Select(_ => sut.GetMovieAsync(603, CancellationToken.None)).ToArray();
        gate.SetResult();
        await Task.WhenAll(calls);

        _inner.Verify(s => s.GetMovieAsync(603, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMovieAsync_LogsCacheMissWithDetailsKey()
    {
        _inner.Setup(s => s.GetMovieAsync(603, It.IsAny<CancellationToken>())).ReturnsAsync(Details(603));
        var logger = new Mock<ILogger<CachedMovieService>>();
        var sut = CreateSut(logger: logger.Object);

        await sut.GetMovieAsync(603, CancellationToken.None);
        await sut.GetMovieAsync(603, CancellationToken.None);

        logger.Verify(l => l.Log(
            LogLevel.Debug,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("v1:movie:603;")),
            null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    // =====================================================
    // GetTrailerVideoKeysAsync
    // =====================================================

    [Fact]
    public async Task GetTrailerVideoKeysAsync_OnMiss_CallsInnerWithIdAndReturnsItsResult()
    {
        _inner.Setup(s => s.GetTrailerVideoKeysAsync(603, It.IsAny<CancellationToken>())).ReturnsAsync(Trailers());
        var sut = CreateSut();

        var result = await sut.GetTrailerVideoKeysAsync(603, CancellationToken.None);

        result.Should().BeEquivalentTo(Trailers());
        _inner.Verify(s => s.GetTrailerVideoKeysAsync(603, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTrailerVideoKeysAsync_OnHit_DoesNotCallInnerAgain()
    {
        _inner.Setup(s => s.GetTrailerVideoKeysAsync(603, It.IsAny<CancellationToken>())).ReturnsAsync(Trailers());
        var sut = CreateSut();

        var first = await sut.GetTrailerVideoKeysAsync(603, CancellationToken.None);
        var second = await sut.GetTrailerVideoKeysAsync(603, CancellationToken.None);

        second.Should().BeEquivalentTo(first);
        _inner.Verify(s => s.GetTrailerVideoKeysAsync(603, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTrailerVideoKeysAsync_EmptyResultIsCached()
    {
        _inner.Setup(s => s.GetTrailerVideoKeysAsync(603, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MovieTrailerVideoKeys>());
        var sut = CreateSut();

        var first = await sut.GetTrailerVideoKeysAsync(603, CancellationToken.None);
        var second = await sut.GetTrailerVideoKeysAsync(603, CancellationToken.None);

        first.Should().BeEmpty();
        second.Should().BeEmpty();
        _inner.Verify(s => s.GetTrailerVideoKeysAsync(603, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTrailerVideoKeysAsync_DifferentIds_AreCachedSeparately()
    {
        _inner.Setup(s => s.GetTrailerVideoKeysAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) => Trailers($"key-{id}"));
        var sut = CreateSut();

        var a = await sut.GetTrailerVideoKeysAsync(1, CancellationToken.None);
        var b = await sut.GetTrailerVideoKeysAsync(2, CancellationToken.None);
        var a2 = await sut.GetTrailerVideoKeysAsync(1, CancellationToken.None);

        a.Single().Key.Should().Be("key-1");
        b.Single().Key.Should().Be("key-2");
        a2.Single().Key.Should().Be("key-1");
        _inner.Verify(s => s.GetTrailerVideoKeysAsync(1, It.IsAny<CancellationToken>()), Times.Once);
        _inner.Verify(s => s.GetTrailerVideoKeysAsync(2, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTrailerVideoKeysAsync_WhenInnerThrows_PropagatesAndDoesNotCacheTheFailure()
    {
        _inner.SetupSequence(s => s.GetTrailerVideoKeysAsync(603, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("connection refused"))
            .ReturnsAsync(Trailers());
        var sut = CreateSut();

        await Assert.ThrowsAsync<HttpRequestException>(() => sut.GetTrailerVideoKeysAsync(603, CancellationToken.None));
        var recovered = await sut.GetTrailerVideoKeysAsync(603, CancellationToken.None);

        recovered.Should().BeEquivalentTo(Trailers());
        _inner.Verify(s => s.GetTrailerVideoKeysAsync(603, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GetTrailerVideoKeysAsync_ConcurrentCallsForSameId_CallInnerOnlyOnce()
    {
        var gate = new TaskCompletionSource();
        _inner.Setup(s => s.GetTrailerVideoKeysAsync(603, It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await gate.Task;
                return Trailers();
            });
        var sut = CreateSut();

        var calls = Enumerable.Range(0, 10).Select(_ => sut.GetTrailerVideoKeysAsync(603, CancellationToken.None)).ToArray();
        gate.SetResult();
        await Task.WhenAll(calls);

        _inner.Verify(s => s.GetTrailerVideoKeysAsync(603, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTrailerVideoKeysAsync_LogsCacheMissWithVideosKey()
    {
        _inner.Setup(s => s.GetTrailerVideoKeysAsync(603, It.IsAny<CancellationToken>())).ReturnsAsync(Trailers());
        var logger = new Mock<ILogger<CachedMovieService>>();
        var sut = CreateSut(logger: logger.Object);

        await sut.GetTrailerVideoKeysAsync(603, CancellationToken.None);

        logger.Verify(l => l.Log(
            LogLevel.Debug,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("v1:movie:603:videos")),
            null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    // =====================================================
    // Cross-method isolation
    // =====================================================

    [Fact]
    public async Task DetailsAndTrailersForSameMovieId_UseSeparateCacheEntries()
    {
        _inner.Setup(s => s.GetMovieAsync(603, It.IsAny<CancellationToken>())).ReturnsAsync(Details(603));
        _inner.Setup(s => s.GetTrailerVideoKeysAsync(603, It.IsAny<CancellationToken>())).ReturnsAsync(Trailers());
        var sut = CreateSut();

        var details = await sut.GetMovieAsync(603, CancellationToken.None);
        var trailers = await sut.GetTrailerVideoKeysAsync(603, CancellationToken.None);
        var detailsAgain = await sut.GetMovieAsync(603, CancellationToken.None);
        var trailersAgain = await sut.GetTrailerVideoKeysAsync(603, CancellationToken.None);

        details.Title.Should().Be("Movie 603");
        trailers.Single().Key.Should().Be("abc123");
        detailsAgain.Should().BeEquivalentTo(details);
        trailersAgain.Should().BeEquivalentTo(trailers);
        _inner.Verify(s => s.GetMovieAsync(603, It.IsAny<CancellationToken>()), Times.Once);
        _inner.Verify(s => s.GetTrailerVideoKeysAsync(603, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CachedEntriesAreSharedAcrossServiceInstancesUsingTheSameCache()
    {
        _inner.Setup(s => s.GetMovieAsync(603, It.IsAny<CancellationToken>())).ReturnsAsync(Details(603));
        var first = CreateSut();
        var second = CreateSut();

        await first.GetMovieAsync(603, CancellationToken.None);
        await second.GetMovieAsync(603, CancellationToken.None);

        _inner.Verify(s => s.GetMovieAsync(603, It.IsAny<CancellationToken>()), Times.Once);
    }
}
