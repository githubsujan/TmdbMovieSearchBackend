using System.Net;
using System.Text;
using FluentAssertions;
using MovieSearchCase.Domain.Entities;
using MovieSearchCase.Domain.Exceptions;
using MovieSearchCase.Infrastructure.Clients;
using Xunit;

namespace MovieSearchCase.Infrastructure.Tests.Clients;

public class TmdbClientTests
{
    private const string BaseUrl = "https://api.themoviedb.org/3/";

    // ---------- helpers ----------

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return respond(request, cancellationToken);
        }
    }

    private static StubHttpMessageHandler Respond(HttpStatusCode status, string? json = null) =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(json ?? "{}", Encoding.UTF8, "application/json"),
        }));

    private static StubHttpMessageHandler NeverAnswer() =>
        new(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

    private static TmdbClient CreateClient(HttpMessageHandler handler, TimeSpan? httpClientTimeout = null)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri(BaseUrl) };
        if (httpClientTimeout is not null)
        {
            httpClient.Timeout = httpClientTimeout.Value;
        }

        return new TmdbClient(httpClient);
    }

    private static async Task<MovieException> ShouldThrowMovieException(Func<Task> act) =>
        (await act.Should().ThrowAsync<MovieException>()).Which;

    private const string TwoMoviesJson = """
        {
          "page": 1,
          "total_pages": 5,
          "total_results": 100,
          "results": [
            {
              "id": 268,
              "title": "Batman",
              "overview": "Batman must face his most ruthless nemesis.",
              "poster_path": "/poster.jpg",
              "backdrop_path": "/backdrop.jpg",
              "vote_average": 7.2,
              "release_date": "1989-06-21"
            },
            {
              "id": 269,
              "title": "No Date Movie",
              "overview": null,
              "poster_path": null,
              "backdrop_path": null,
              "vote_average": 0,
              "release_date": ""
            }
          ]
        }
        """;

    private static void AssertTwoMoviesMapped(IReadOnlyList<Movie> movies)
    {
        movies.Should().HaveCount(2);
        movies[0].Should().BeEquivalentTo(new Movie
        {
            Id = 268,
            Title = "Batman",
            Overview = "Batman must face his most ruthless nemesis.",
            PosterPath = "/poster.jpg",
            BackdropPath = "/backdrop.jpg",
            VoteAverage = 7.2,
            ReleaseDate = new DateOnly(1989, 6, 21),
        });
        movies[1].ReleaseDate.Should().BeNull();
        movies[1].Overview.Should().BeNull();
        movies[1].PosterPath.Should().BeNull();
        movies[1].BackdropPath.Should().BeNull();
    }

    // =====================================================================
    // GetTrendingMoviesAsync
    // =====================================================================

    [Fact]
    public async Task GetTrendingMoviesAsyncShouldMapTmdbResponseToDomainModels()
    {
        var client = CreateClient(Respond(HttpStatusCode.OK, TwoMoviesJson));

        var result = await client.GetTrendingMoviesAsync(CancellationToken.None);

        AssertTwoMoviesMapped(result);
    }

    [Fact]
    public async Task GetTrendingMoviesAsyncShouldReturnEmptyListWhenTmdbReturnsNoResults()
    {
        const string json = """{ "page": 1, "total_pages": 0, "total_results": 0, "results": [] }""";
        var client = CreateClient(Respond(HttpStatusCode.OK, json));

        var result = await client.GetTrendingMoviesAsync(CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTrendingMoviesAsyncShouldCallWeeklyTrendingEndpoint()
    {
        var handler = Respond(HttpStatusCode.OK, TwoMoviesJson);
        var client = CreateClient(handler);

        await client.GetTrendingMoviesAsync(CancellationToken.None);

        var request = handler.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Get);
        request.RequestUri!.PathAndQuery.Should().Be("/3/trending/movie/week");
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task GetTrendingMoviesAsyncShouldThrowServiceUnavailableForAnyErrorStatus(HttpStatusCode status)
    {
        var client = CreateClient(Respond(status));

        var exception = await ShouldThrowMovieException(() => client.GetTrendingMoviesAsync(CancellationToken.None));

        exception.Title.Should().Be(MovieException.ExceptionTitle);
        exception.ErrorCode.Should().Be(ErrorType.UpstreamServiceUnavailable);
        exception.Message.Should().Be("TMDB did not return trending movies.");
    }

    [Fact]
    public async Task GetTrendingMoviesAsyncShouldThrowServiceUnavailableWhenConnectionFails()
    {
        var handler = new StubHttpMessageHandler((_, _) => throw new HttpRequestException("connection refused"));
        var client = CreateClient(handler);

        var exception = await ShouldThrowMovieException(() => client.GetTrendingMoviesAsync(CancellationToken.None));

        exception.ErrorCode.Should().Be(ErrorType.UpstreamServiceUnavailable);
        exception.Message.Should().Be("TMDB did not return trending movies.");
    }

    [Fact]
    public async Task GetTrendingMoviesAsyncShouldThrowServiceUnavailableWhenTmdbReturnsEmptyBody()
    {
        // The JSON literal `null` deserializes to a null response object.
        var client = CreateClient(Respond(HttpStatusCode.OK, "null"));

        var exception = await ShouldThrowMovieException(() => client.GetTrendingMoviesAsync(CancellationToken.None));

        exception.Title.Should().Be(MovieException.ExceptionTitle);
        exception.ErrorCode.Should().Be(ErrorType.UpstreamServiceUnavailable);
        exception.Message.Should().Be("TMDB returned an empty trending movies response.");
    }

    [Fact]
    public async Task GetTrendingMoviesAsyncShouldRethrowCancellationWhenCallerCancels()
    {
        var client = CreateClient(NeverAnswer());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        Func<Task> act = () => client.GetTrendingMoviesAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // =====================================================================
    // SearchMoviesAsync
    // =====================================================================

    [Fact]
    public async Task SearchMoviesAsyncShouldMapTmdbResponseToDomainModel()
    {
        var client = CreateClient(Respond(HttpStatusCode.OK, TwoMoviesJson));

        var result = await client.SearchMoviesAsync(new MovieSearchRequest("batman", 1), CancellationToken.None);

        result.Page.Should().Be(1);
        result.TotalPages.Should().Be(5);
        result.TotalResults.Should().Be(100);
        AssertTwoMoviesMapped(result.Results);
    }

    [Fact]
    public async Task SearchMoviesAsyncShouldReturnEmptyResultsWhenTmdbHasNoMatches()
    {
        const string json = """{ "page": 1, "total_pages": 0, "total_results": 0, "results": [] }""";
        var client = CreateClient(Respond(HttpStatusCode.OK, json));

        var result = await client.SearchMoviesAsync(new MovieSearchRequest("zzzzzz", 1), CancellationToken.None);

        result.Page.Should().Be(1);
        result.TotalPages.Should().Be(0);
        result.TotalResults.Should().Be(0);
        result.Results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchMoviesAsyncShouldCallSearchEndpointWithEscapedQueryAndPage()
    {
        const string json = """{ "page": 3, "total_pages": 0, "total_results": 0, "results": [] }""";
        var handler = Respond(HttpStatusCode.OK, json);
        var client = CreateClient(handler);

        await client.SearchMoviesAsync(new MovieSearchRequest("batman & robin?", 3), CancellationToken.None);

        var request = handler.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Get);
        request.RequestUri!.PathAndQuery.Should().Be("/3/search/movie?query=batman%20%26%20robin%3F&page=3");
    }

    [Fact]
    public async Task SearchMoviesAsyncShouldThrowEntityNotFoundWhenTmdbReturns404()
    {
        var client = CreateClient(Respond(HttpStatusCode.NotFound));

        var exception = await ShouldThrowMovieException(
            () => client.SearchMoviesAsync(new MovieSearchRequest("batman", 1), CancellationToken.None));

        exception.Title.Should().Be(ExceptionTitleDetail.NotFoundTitle);
        exception.ErrorCode.Should().Be(ErrorType.EntityNotFound);
        exception.Message.Should().Be(ExceptionTitleDetail.NotFoundDetail);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task SearchMoviesAsyncShouldThrowServiceUnavailableWhenTmdbReturnsOtherErrorStatus(HttpStatusCode status)
    {
        var client = CreateClient(Respond(status));

        var exception = await ShouldThrowMovieException(
            () => client.SearchMoviesAsync(new MovieSearchRequest("batman", 1), CancellationToken.None));

        exception.Title.Should().Be(MovieException.ExceptionTitle);
        exception.ErrorCode.Should().Be(ErrorType.UpstreamServiceUnavailable);
        exception.Message.Should().Be(ExceptionTitleDetail.ServiceUnavailableDetail);
    }

    [Fact]
    public async Task SearchMoviesAsyncShouldThrowServiceUnavailableWhenConnectionFails()
    {
        var handler = new StubHttpMessageHandler((_, _) => throw new HttpRequestException("connection refused"));
        var client = CreateClient(handler);

        var exception = await ShouldThrowMovieException(
            () => client.SearchMoviesAsync(new MovieSearchRequest("batman", 1), CancellationToken.None));

        exception.ErrorCode.Should().Be(ErrorType.UpstreamServiceUnavailable);
        exception.Message.Should().Be(ExceptionTitleDetail.ServiceUnavailableDetail);
    }

    [Fact]
    public async Task SearchMoviesAsyncShouldThrowServiceUnavailableWhenTmdbReturnsEmptyBody()
    {
        var client = CreateClient(Respond(HttpStatusCode.OK, "null"));

        var exception = await ShouldThrowMovieException(
            () => client.SearchMoviesAsync(new MovieSearchRequest("batman", 1), CancellationToken.None));

        exception.Title.Should().Be(MovieException.ExceptionTitle);
        exception.ErrorCode.Should().Be(ErrorType.UpstreamServiceUnavailable);
        exception.Message.Should().Be(ExceptionTitleDetail.EmptyResponseDetail);
    }

    [Fact]
    public async Task SearchMoviesAsyncShouldThrowTimeoutWhenRequestIsCancelledWithoutCallerCancelling()
    {
        var handler = new StubHttpMessageHandler((_, _) => throw new TaskCanceledException("timed out"));
        var client = CreateClient(handler);

        var exception = await ShouldThrowMovieException(
            () => client.SearchMoviesAsync(new MovieSearchRequest("batman", 1), CancellationToken.None));

        exception.Title.Should().Be(MovieException.ExceptionTitle);
        exception.ErrorCode.Should().Be(ErrorType.UpstreamServiceUnavailable);
        exception.Message.Should().Be(ExceptionTitleDetail.TimeoutDetail);
    }

    [Fact]
    public async Task SearchMoviesAsyncShouldThrowTimeoutWhenHttpClientTimeoutElapses()
    {
        var client = CreateClient(NeverAnswer(), httpClientTimeout: TimeSpan.FromMilliseconds(100));

        var exception = await ShouldThrowMovieException(
            () => client.SearchMoviesAsync(new MovieSearchRequest("batman", 1), CancellationToken.None));

        exception.ErrorCode.Should().Be(ErrorType.UpstreamServiceUnavailable);
        exception.Message.Should().Be(ExceptionTitleDetail.TimeoutDetail);
    }

    [Fact]
    public async Task SearchMoviesAsyncShouldRethrowCancellationWhenCallerCancels()
    {
        var client = CreateClient(NeverAnswer());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        Func<Task> act = () => client.SearchMoviesAsync(new MovieSearchRequest("batman", 1), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

     // =====================================================================
    // GetMovieAsync
    // =====================================================================

    private const string MovieDetailsJson = """
        {
          "id": 268,
          "title": "Batman",
          "overview": "Batman must face his most ruthless nemesis.",
          "poster_path": "/poster.jpg",
          "backdrop_path": "/backdrop.jpg",
          "vote_average": 7.2,
          "release_date": "1989-06-21",
          "runtime": 126,
          "tagline": "Have you ever danced with the devil in the pale moonlight?",
          "genres": [ { "id": 14, "name": "Fantasy" }, { "id": 28, "name": "Action" } ]
        }
        """;

    [Fact]
    public async Task GetMovieAsyncShouldMapTmdbResponseToDomainModel()
    {
        var client = CreateClient(Respond(HttpStatusCode.OK, MovieDetailsJson));

        var result = await client.GetMovieAsync(268, CancellationToken.None);

        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(
            new { Id = 268, Title = "Batman" },
            options => options.ExcludingMissingMembers());
    }

    [Fact]
    public async Task GetMovieAsyncShouldMapExtendedFields()
    {
        var client = CreateClient(Respond(HttpStatusCode.OK, MovieDetailsJson));

        var result = await client.GetMovieAsync(268, CancellationToken.None);

        result.Should().BeEquivalentTo(
            new
            {
                Id = 268,
                Title = "Batman",
                PosterPath = "/poster.jpg",
                BackdropPath = "/backdrop.jpg",
                VoteAverage = 7.2,
                ReleaseDate = DateOnly.ParseExact("1989-06-21", "yyyy-MM-dd"),
                Overview = "Batman must face his most ruthless nemesis.",
                Runtime = 126,
                Tagline = "Have you ever danced with the devil in the pale moonlight?",
                Genres = new List<Genre> { new Genre(14,"Fantasy"), new Genre(28,"Action") },
            },
            options => options.ExcludingMissingMembers());
    }

    [Fact]
    public async Task GetMovieAsyncShouldCallMovieEndpointWithId()
    {
        var handler = Respond(HttpStatusCode.OK, MovieDetailsJson);
        var client = CreateClient(handler);

        await client.GetMovieAsync(268, CancellationToken.None);

        var request = handler.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Get);
        request.RequestUri!.PathAndQuery.Should().Be("/3/movie/268");
    }

    [Fact]
    public async Task GetMovieAsyncShouldThrowEntityNotFoundWhenTmdbReturns404()
    {
        var client = CreateClient(Respond(HttpStatusCode.NotFound));

        var exception = await ShouldThrowMovieException(() => client.GetMovieAsync(999999, CancellationToken.None));

        exception.Title.Should().Be(ExceptionTitleDetail.NotFoundTitle);
        exception.ErrorCode.Should().Be(ErrorType.EntityNotFound);
        exception.Message.Should().Be(ExceptionTitleDetail.NotFoundDetail);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task GetMovieAsyncShouldThrowServiceUnavailableWhenTmdbReturnsOtherErrorStatus(HttpStatusCode status)
    {
        var client = CreateClient(Respond(status));

        var exception = await ShouldThrowMovieException(() => client.GetMovieAsync(268, CancellationToken.None));

        exception.Title.Should().Be(MovieException.ExceptionTitle);
        exception.ErrorCode.Should().Be(ErrorType.UpstreamServiceUnavailable);
        exception.Message.Should().Be(ExceptionTitleDetail.ServiceUnavailableDetail);
    }

    [Fact]
    public async Task GetMovieAsyncShouldThrowServiceUnavailableWhenConnectionFails()
    {
        var handler = new StubHttpMessageHandler((_, _) => throw new HttpRequestException("connection refused"));
        var client = CreateClient(handler);

        var exception = await ShouldThrowMovieException(() => client.GetMovieAsync(268, CancellationToken.None));

        exception.ErrorCode.Should().Be(ErrorType.UpstreamServiceUnavailable);
        exception.Message.Should().Be(ExceptionTitleDetail.ServiceUnavailableDetail);
    }

    [Fact]
    public async Task GetMovieAsyncShouldThrowEntityNotFoundWhenTmdbReturnsEmptyBody()
    {
        var client = CreateClient(Respond(HttpStatusCode.OK, "null"));

        var exception = await ShouldThrowMovieException(() => client.GetMovieAsync(268, CancellationToken.None));

        exception.Title.Should().Be(MovieException.ExceptionTitle);
        exception.ErrorCode.Should().Be(ErrorType.EntityNotFound);
        exception.Message.Should().Be(ExceptionTitleDetail.EmptyResponseDetail);
    }

    [Fact]
    public async Task GetMovieAsyncShouldThrowTimeoutWhenRequestIsCancelledWithoutCallerCancelling()
    {
        var handler = new StubHttpMessageHandler((_, _) => throw new TaskCanceledException("timed out"));
        var client = CreateClient(handler);

        var exception = await ShouldThrowMovieException(() => client.GetMovieAsync(268, CancellationToken.None));

        exception.Title.Should().Be(MovieException.ExceptionTitle);
        exception.ErrorCode.Should().Be(ErrorType.UpstreamServiceUnavailable);
        exception.Message.Should().Be(ExceptionTitleDetail.TimeoutDetail);
    }

    [Fact]
    public async Task GetMovieAsyncShouldThrowTimeoutWhenHttpClientTimeoutElapses()
    {
        var client = CreateClient(NeverAnswer(), httpClientTimeout: TimeSpan.FromMilliseconds(100));

        var exception = await ShouldThrowMovieException(() => client.GetMovieAsync(268, CancellationToken.None));

        exception.ErrorCode.Should().Be(ErrorType.UpstreamServiceUnavailable);
        exception.Message.Should().Be(ExceptionTitleDetail.TimeoutDetail);
    }

    [Fact]
    public async Task GetMovieAsyncShouldRethrowCancellationWhenCallerCancels()
    {
        var client = CreateClient(NeverAnswer());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        Func<Task> act = () => client.GetMovieAsync(268, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // =====================================================================
    // GetTrailerVideoKeysAsync
    // =====================================================================

    private const string VideosJson = """
        {
          "id": 268,
          "results": [
            { "key": "dgC9Q0uhX70", "name": "Official Trailer", "site": "YouTube", "type": "Trailer" },
            { "key": "abc123XYZ",   "name": "Teaser",           "site": "Vimeo",   "type": "Teaser" }
          ]
        }
        """;

    [Fact]
    public async Task GetTrailerVideoKeysAsyncShouldMapTmdbVideos()
    {
        var client = CreateClient(Respond(HttpStatusCode.OK, VideosJson));

        var result = await client.GetTrailerVideoKeysAsync(268, CancellationToken.None);

        result.Should().BeEquivalentTo(
            new[]
            {
                new { Key = "dgC9Q0uhX70", Name = "Official Trailer", Site = "YouTube", Type = "Trailer" },
                new { Key = "abc123XYZ", Name = "Teaser", Site = "Vimeo", Type = "Teaser" },
            },
            options => options.ExcludingMissingMembers().WithStrictOrdering());
    }

    [Fact]
    public async Task GetTrailerVideoKeysAsyncShouldReturnEmptyListWhenMovieHasNoVideos()
    {
        var client = CreateClient(Respond(HttpStatusCode.OK, """{ "id": 268, "results": [] }"""));

        var result = await client.GetTrailerVideoKeysAsync(268, CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTrailerVideoKeysAsyncShouldReturnEmptyListWhenResultsIsNull()
    {
        var client = CreateClient(Respond(HttpStatusCode.OK, """{ "id": 268, "results": null }"""));

        var result = await client.GetTrailerVideoKeysAsync(268, CancellationToken.None);

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTrailerVideoKeysAsyncShouldCallVideosEndpointWithId()
    {
        var handler = Respond(HttpStatusCode.OK, VideosJson);
        var client = CreateClient(handler);

        await client.GetTrailerVideoKeysAsync(268, CancellationToken.None);

        var request = handler.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Get);
        request.RequestUri!.PathAndQuery.Should().Be("/3/movie/268/videos");
    }

    [Fact]
    public async Task GetTrailerVideoKeysAsyncShouldThrowEntityNotFoundWhenTmdbReturns404()
    {
        var client = CreateClient(Respond(HttpStatusCode.NotFound));

        var exception = await ShouldThrowMovieException(() => client.GetTrailerVideoKeysAsync(999999, CancellationToken.None));

        exception.Title.Should().Be(ExceptionTitleDetail.NotFoundTitle);
        exception.ErrorCode.Should().Be(ErrorType.EntityNotFound);
        exception.Message.Should().Be(ExceptionTitleDetail.NotFoundDetail);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task GetTrailerVideoKeysAsyncShouldThrowServiceUnavailableWhenTmdbReturnsOtherErrorStatus(HttpStatusCode status)
    {
        var client = CreateClient(Respond(status));

        var exception = await ShouldThrowMovieException(() => client.GetTrailerVideoKeysAsync(268, CancellationToken.None));

        exception.Title.Should().Be(MovieException.ExceptionTitle);
        exception.ErrorCode.Should().Be(ErrorType.UpstreamServiceUnavailable);
        exception.Message.Should().Be(ExceptionTitleDetail.ServiceUnavailableDetail);
    }

    [Fact]
    public async Task GetTrailerVideoKeysAsyncShouldThrowServiceUnavailableWhenConnectionFails()
    {
        var handler = new StubHttpMessageHandler((_, _) => throw new HttpRequestException("connection refused"));
        var client = CreateClient(handler);

        var exception = await ShouldThrowMovieException(() => client.GetTrailerVideoKeysAsync(268, CancellationToken.None));

        exception.ErrorCode.Should().Be(ErrorType.UpstreamServiceUnavailable);
        exception.Message.Should().Be(ExceptionTitleDetail.ServiceUnavailableDetail);
    }

    [Fact]
    public async Task GetTrailerVideoKeysAsyncShouldThrowEntityNotFoundWhenTmdbReturnsEmptyBody()
    {
        var client = CreateClient(Respond(HttpStatusCode.OK, "null"));

        var exception = await ShouldThrowMovieException(() => client.GetTrailerVideoKeysAsync(268, CancellationToken.None));

        exception.Title.Should().Be(MovieException.ExceptionTitle);
        exception.ErrorCode.Should().Be(ErrorType.EntityNotFound);
        exception.Message.Should().Be(ExceptionTitleDetail.EmptyResponseDetail);
    }

    [Fact]
    public async Task GetTrailerVideoKeysAsyncShouldThrowTimeoutWhenRequestIsCancelledWithoutCallerCancelling()
    {
        var handler = new StubHttpMessageHandler((_, _) => throw new TaskCanceledException("timed out"));
        var client = CreateClient(handler);

        var exception = await ShouldThrowMovieException(() => client.GetTrailerVideoKeysAsync(268, CancellationToken.None));

        exception.Title.Should().Be(MovieException.ExceptionTitle);
        exception.ErrorCode.Should().Be(ErrorType.UpstreamServiceUnavailable);
        exception.Message.Should().Be(ExceptionTitleDetail.TimeoutDetail);
    }

    [Fact]
    public async Task GetTrailerVideoKeysAsyncShouldThrowTimeoutWhenHttpClientTimeoutElapses()
    {
        var client = CreateClient(NeverAnswer(), httpClientTimeout: TimeSpan.FromMilliseconds(100));

        var exception = await ShouldThrowMovieException(() => client.GetTrailerVideoKeysAsync(268, CancellationToken.None));

        exception.ErrorCode.Should().Be(ErrorType.UpstreamServiceUnavailable);
        exception.Message.Should().Be(ExceptionTitleDetail.TimeoutDetail);
    }

    [Fact]
    public async Task GetTrailerVideoKeysAsyncShouldRethrowCancellationWhenCallerCancels()
    {
        var client = CreateClient(NeverAnswer());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        Func<Task> act = () => client.GetTrailerVideoKeysAsync(268, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

     // =====================================================================
    // SearchTvAsync
    // =====================================================================

    private static async Task<TvException> ShouldThrowTvException(Func<Task> act) =>
        (await act.Should().ThrowAsync<TvException>()).Which;

    private const string TvSearchJson = """
        {
          "page": 2,
          "total_pages": 12,
          "total_results": 231,
          "results": [
            {
              "id": 1396,
              "name": "Breaking Bad",
              "overview": "A chemistry teacher turns to crime.",
              "poster_path": "/poster.jpg",
              "backdrop_path": "/backdrop.jpg",
              "vote_average": 8.9,
              "first_air_date": "2008-01-20"
            },
            {
              "id": 1399,
              "name": "No Date Show",
              "overview": null,
              "poster_path": null,
              "backdrop_path": null,
              "vote_average": 0,
              "first_air_date": ""
            }
          ]
        }
        """;

    private static TvSearchRequest TvRequest(string query = "breaking", int page = 1, int? year = null) =>
        new(query, page, year);

    [Fact]
    public async Task SearchTvAsyncShouldMapTmdbResponseToDomainModel()
    {
        var client = CreateClient(Respond(HttpStatusCode.OK, TvSearchJson));

        var result = await client.SearchTvAsync(TvRequest(page: 2), CancellationToken.None);

        result.Page.Should().Be(2);
        result.TotalPages.Should().Be(12);
        result.TotalResults.Should().Be(231);
        result.Results.Should().HaveCount(2);
        result.Results.Should().BeEquivalentTo(
            new[]
            {
                new { Id = 1396, Name = "Breaking Bad" },
                new { Id = 1399, Name = "No Date Show" },
            },
            options => options.ExcludingMissingMembers().WithStrictOrdering());
    }

    [Fact]
    public async Task SearchTvAsyncShouldMapFirstAirDateAndNullableFields()
    {
        var client = CreateClient(Respond(HttpStatusCode.OK, TvSearchJson));

        var result = await client.SearchTvAsync(TvRequest(), CancellationToken.None);

        result.Results[0].Should().BeEquivalentTo(
            new
            {
                FirstAirDate = new DateOnly(2008, 1, 20),
                Overview = "A chemistry teacher turns to crime.",
                PosterPath = "/poster.jpg",
            },
            options => options.ExcludingMissingMembers());

        result.Results[1].Should().BeEquivalentTo(
            new { FirstAirDate = (DateOnly?)null, Overview = (string?)null, PosterPath = (string?)null },
            options => options.ExcludingMissingMembers());
    }

    [Fact]
    public async Task SearchTvAsyncShouldReturnEmptyResultsWhenTmdbHasNoMatches()
    {
        const string json = """{ "page": 1, "total_pages": 0, "total_results": 0, "results": [] }""";
        var client = CreateClient(Respond(HttpStatusCode.OK, json));

        var result = await client.SearchTvAsync(TvRequest("zzzzzz"), CancellationToken.None);

        result.Page.Should().Be(1);
        result.TotalPages.Should().Be(0);
        result.TotalResults.Should().Be(0);
        result.Results.Should().BeEmpty();
    }

    // ---------- outgoing request ----------

    [Fact]
    public async Task SearchTvAsyncShouldOmitFirstAirDateYearWhenItIsNull()
    {
        var handler = Respond(HttpStatusCode.OK, """{ "page": 1, "total_pages": 0, "total_results": 0, "results": [] }""");
        var client = CreateClient(handler);

        await client.SearchTvAsync(TvRequest("breaking", 1, year: null), CancellationToken.None);

        var request = handler.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Get);
        request.RequestUri!.PathAndQuery.Should().Be("/3/search/tv?query=breaking&page=1");
        request.RequestUri.Query.Should().NotContain("first_air_date_year");
    }

    [Fact]
    public async Task SearchTvAsyncShouldAppendFirstAirDateYearWhenItHasAValue()
    {
        var handler = Respond(HttpStatusCode.OK, """{ "page": 3, "total_pages": 0, "total_results": 0, "results": [] }""");
        var client = CreateClient(handler);

        await client.SearchTvAsync(TvRequest("breaking", 3, year: 2008), CancellationToken.None);

        var request = handler.Requests.Should().ContainSingle().Subject;
        request.RequestUri!.PathAndQuery.Should().Be("/3/search/tv?query=breaking&page=3&first_air_date_year=2008");
    }

    [Fact]
    public async Task SearchTvAsyncShouldEscapeTheQuery()
    {
        var handler = Respond(HttpStatusCode.OK, """{ "page": 1, "total_pages": 0, "total_results": 0, "results": [] }""");
        var client = CreateClient(handler);

        await client.SearchTvAsync(TvRequest("breaking & bad?", 1, 2008), CancellationToken.None);

        var request = handler.Requests.Should().ContainSingle().Subject;
        request.RequestUri!.PathAndQuery.Should()
            .Be("/3/search/tv?query=breaking%20%26%20bad%3F&page=1&first_air_date_year=2008");
    }

    // ---------- TMDB errors ----------

    [Fact]
    public async Task SearchTvAsyncShouldThrowEntityNotFoundWhenTmdbReturns404()
    {
        var client = CreateClient(Respond(HttpStatusCode.NotFound));

        var exception = await ShouldThrowTvException(() => client.SearchTvAsync(TvRequest(), CancellationToken.None));

        exception.Title.Should().Be(ExceptionTitleDetail.NotFoundTitle);
        exception.ErrorCode.Should().Be(ErrorType.EntityNotFound);
        exception.Message.Should().Be(ExceptionTitleDetail.NotFoundDetail);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task SearchTvAsyncShouldThrowServiceUnavailableWhenTmdbReturnsOtherErrorStatus(HttpStatusCode status)
    {
        var client = CreateClient(Respond(status));

        var exception = await ShouldThrowTvException(() => client.SearchTvAsync(TvRequest(), CancellationToken.None));

        exception.Title.Should().Be(TvException.ExceptionTitle);
        exception.ErrorCode.Should().Be(ErrorType.UpstreamServiceUnavailable);
        exception.Message.Should().Be(ExceptionTitleDetail.ServiceUnavailableDetail);
    }

    [Fact]
    public async Task SearchTvAsyncShouldThrowServiceUnavailableWhenConnectionFails()
    {
        var handler = new StubHttpMessageHandler((_, _) => throw new HttpRequestException("connection refused"));
        var client = CreateClient(handler);

        var exception = await ShouldThrowTvException(() => client.SearchTvAsync(TvRequest(), CancellationToken.None));

        exception.Title.Should().Be(TvException.ExceptionTitle);
        exception.ErrorCode.Should().Be(ErrorType.UpstreamServiceUnavailable);
        exception.Message.Should().Be(ExceptionTitleDetail.ServiceUnavailableDetail);
    }

    [Fact]
    public async Task SearchTvAsyncShouldThrowEntityNotFoundWhenTmdbReturnsEmptyBody()
    {
        var client = CreateClient(Respond(HttpStatusCode.OK, "null"));

        var exception = await ShouldThrowTvException(() => client.SearchTvAsync(TvRequest(), CancellationToken.None));

        exception.Title.Should().Be(TvException.ExceptionTitle);
        exception.ErrorCode.Should().Be(ErrorType.EntityNotFound);
        exception.Message.Should().Be(ExceptionTitleDetail.EmptyResponseDetail);
    }

    // ---------- timeout & cancellation ----------

    [Fact]
    public async Task SearchTvAsyncShouldThrowTimeoutWhenRequestIsCancelledWithoutCallerCancelling()
    {
        var handler = new StubHttpMessageHandler((_, _) => throw new TaskCanceledException("timed out"));
        var client = CreateClient(handler);

        var exception = await ShouldThrowTvException(() => client.SearchTvAsync(TvRequest(), CancellationToken.None));

        exception.Title.Should().Be(TvException.ExceptionTitle);
        exception.ErrorCode.Should().Be(ErrorType.UpstreamServiceUnavailable);
        exception.Message.Should().Be(ExceptionTitleDetail.TimeoutDetail);
    }

    [Fact]
    public async Task SearchTvAsyncShouldThrowTimeoutWhenHttpClientTimeoutElapses()
    {
        var client = CreateClient(NeverAnswer(), httpClientTimeout: TimeSpan.FromMilliseconds(100));

        var exception = await ShouldThrowTvException(() => client.SearchTvAsync(TvRequest(), CancellationToken.None));

        exception.ErrorCode.Should().Be(ErrorType.UpstreamServiceUnavailable);
        exception.Message.Should().Be(ExceptionTitleDetail.TimeoutDetail);
    }

    [Fact]
    public async Task SearchTvAsyncShouldRethrowCancellationWhenCallerCancels()
    {
        var client = CreateClient(NeverAnswer());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        Func<Task> act = () => client.SearchTvAsync(TvRequest(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}