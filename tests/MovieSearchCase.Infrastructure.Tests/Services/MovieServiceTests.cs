using FluentAssertions;
using Moq;
using MovieSearchCase.Domain.Entities;
using MovieSearchCase.Domain.Exceptions;
using MovieSearchCase.Domain.Interfaces.Clients;
using MovieSearchCase.Infrastructure.Services;
using Xunit;

namespace MovieSearchCase.Infrastructure.Tests.Services;

public class MovieServiceTests
{
    private readonly Mock<ITmdbClient> _tmdbClientMock = new();
    private readonly MovieService _movieService;

    public MovieServiceTests()
    {
        _movieService = new MovieService(_tmdbClientMock.Object);
    }

    [Fact]
    public async Task GetTrendingAsyncShouldReturnMoviesFromTmdbClient()
    {
        var expectedMovies = new List<Movie>
        {
            new()
            {
                Id = 42,
                Title = "The Answer",
                VoteAverage = 9.2,
            },
        };
        _tmdbClientMock
            .Setup(client => client.GetTrendingMoviesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedMovies);
        var result = await _movieService.GetTrendingAsync(CancellationToken.None);
        Assert.Same(expectedMovies, result);
    }

     [Fact]
    public async Task SearchMoviesAsyncShouldReturnResponseFromTmdbClient()
    {
        var request = new MovieSearchRequest("batman", 2);
        var expectedResponse = new MovieSearchResponse<Movie>
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
                    VoteAverage = 7.2,
                },
            ],
        };

        _tmdbClientMock
            .Setup(client => client.SearchMoviesAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var result = await _movieService.SearchMoviesAsync(request, CancellationToken.None);

        result.Should().BeSameAs(expectedResponse);
    }

    [Fact]
    public async Task SearchMoviesAsyncShouldPassRequestAndCancellationTokenToTmdbClientUnchanged()
    {
        using var cts = new CancellationTokenSource();
        var request = new MovieSearchRequest("batman", 3);

        _tmdbClientMock
            .Setup(client => client.SearchMoviesAsync(It.IsAny<MovieSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MovieSearchResponse<Movie> { Results = [] });

        await _movieService.SearchMoviesAsync(request, cts.Token);

        _tmdbClientMock.Verify(client => client.SearchMoviesAsync(request, cts.Token), Times.Once);
        _tmdbClientMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SearchMoviesAsyncShouldReturnEmptyResponseWhenTmdbHasNoMatches()
    {
        var emptyResponse = new MovieSearchResponse<Movie>
        {
            Page = 1,
            TotalPages = 0,
            TotalResults = 0,
            Results = [],
        };

        _tmdbClientMock
            .Setup(client => client.SearchMoviesAsync(It.IsAny<MovieSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyResponse);


        var result = await _movieService.SearchMoviesAsync(new MovieSearchRequest("zzzzzz", 1), CancellationToken.None);

        result.TotalResults.Should().Be(0);
        result.TotalPages.Should().Be(0);
        result.Results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchMoviesAsyncShouldPropagateExceptionFromTmdbClient()
    {
        var expected = new MovieException(
            MovieException.ExceptionTitle,
            ErrorType.UpstreamServiceUnavailable,
            ExceptionTitleDetail.TimeoutDetail);

        _tmdbClientMock
            .Setup(client => client.SearchMoviesAsync(It.IsAny<MovieSearchRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expected);

        Func<Task> act = () => _movieService.SearchMoviesAsync(new MovieSearchRequest("batman", 1), CancellationToken.None);

        (await act.Should().ThrowAsync<MovieException>()).Which.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task SearchMoviesAsyncShouldNotSwallowCancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _tmdbClientMock
            .Setup(client => client.SearchMoviesAsync(It.IsAny<MovieSearchRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        Func<Task> act = () => _movieService.SearchMoviesAsync(new MovieSearchRequest("batman", 1), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // =====================================================================
    // GetMovieAsync
    // =====================================================================
    [Fact]
    public async Task GetMovieAsyncShouldReturnMovieFromTmdbClient()
    {
        var expected = It.IsAny<MovieExtendDetails>();
        _tmdbClientMock
            .Setup(client => client.GetMovieAsync(268, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _movieService.GetMovieAsync(268, CancellationToken.None);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GetMovieAsyncShouldPassMovieIdAndCancellationTokenToTmdbClientUnchanged()
    {
        using var cts = new CancellationTokenSource();
        _tmdbClientMock
            .Setup(client => client.GetMovieAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(It.IsAny<MovieExtendDetails>());

        await _movieService.GetMovieAsync(268, cts.Token);

        _tmdbClientMock.Verify(client => client.GetMovieAsync(268, cts.Token), Times.Once);
        _tmdbClientMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetMovieAsyncShouldPropagateNotFoundExceptionFromTmdbClient()
    {
        var expected = new MovieException(
            ExceptionTitleDetail.NotFoundTitle,
            ErrorType.EntityNotFound,
            ExceptionTitleDetail.NotFoundDetail);

        _tmdbClientMock
            .Setup(client => client.GetMovieAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expected);

        Func<Task> act = () => _movieService.GetMovieAsync(999999, CancellationToken.None);

        (await act.Should().ThrowAsync<MovieException>()).Which.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GetMovieAsyncShouldNotSwallowCancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _tmdbClientMock
            .Setup(client => client.GetMovieAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        Func<Task> act = () => _movieService.GetMovieAsync(268, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // =====================================================================
    // GetTrailerVideoKeysAsync
    // =====================================================================

    [Fact]
    public async Task GetTrailerVideoKeysAsyncShouldReturnVideosFromTmdbClient()
    {
        IReadOnlyList<MovieTrailerVideoKeys> expected =
        [
            It.IsAny<MovieTrailerVideoKeys>(),
            It.IsAny<MovieTrailerVideoKeys>(),
        ];

        _tmdbClientMock
            .Setup(client => client.GetTrailerVideoKeysAsync(268, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _movieService.GetTrailerVideoKeysAsync(268, CancellationToken.None);

        result.Should().BeSameAs(expected);
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetTrailerVideoKeysAsyncShouldReturnEmptyListWhenMovieHasNoVideos()
    {
        IReadOnlyList<MovieTrailerVideoKeys> expected = [];

        _tmdbClientMock
            .Setup(client => client.GetTrailerVideoKeysAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _movieService.GetTrailerVideoKeysAsync(268, CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTrailerVideoKeysAsyncShouldPassMovieIdAndCancellationTokenToTmdbClientUnchanged()
    {
        using var cts = new CancellationTokenSource();
        _tmdbClientMock
            .Setup(client => client.GetTrailerVideoKeysAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MovieTrailerVideoKeys>());

        await _movieService.GetTrailerVideoKeysAsync(268, cts.Token);

        _tmdbClientMock.Verify(client => client.GetTrailerVideoKeysAsync(268, cts.Token), Times.Once);
        _tmdbClientMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetTrailerVideoKeysAsyncShouldPropagateExceptionFromTmdbClient()
    {
        var expected = new MovieException(
            MovieException.ExceptionTitle,
            ErrorType.UpstreamServiceUnavailable,
            ExceptionTitleDetail.TimeoutDetail);

        _tmdbClientMock
            .Setup(client => client.GetTrailerVideoKeysAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expected);

        Func<Task> act = () => _movieService.GetTrailerVideoKeysAsync(268, CancellationToken.None);

        (await act.Should().ThrowAsync<MovieException>()).Which.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GetTrailerVideoKeysAsyncShouldNotSwallowCancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _tmdbClientMock
            .Setup(client => client.GetTrailerVideoKeysAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        Func<Task> act = () => _movieService.GetTrailerVideoKeysAsync(268, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
