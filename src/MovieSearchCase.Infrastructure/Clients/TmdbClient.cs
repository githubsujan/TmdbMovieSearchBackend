using System.Net;
using System.Net.Http.Json;
using MovieSearchCase.Domain.Entities;
using MovieSearchCase.Domain.Exceptions;
using MovieSearchCase.Domain.Interfaces.Clients;

namespace MovieSearchCase.Infrastructure.Clients;

public class TmdbClient : ITmdbClient
{
    private readonly HttpClient _httpClient;

    public TmdbClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<Movie>> GetTrendingMoviesAsync(CancellationToken cancellationToken)
    {
        TmdbPagedResponse<TmdbMovie>? response;

        try
        {
            response = await _httpClient.GetFromJsonAsync<TmdbPagedResponse<TmdbMovie>>(
                "trending/movie/week",
                cancellationToken);
        }
        catch (HttpRequestException)
        {
            throw new MovieException(
                MovieException.ExceptionTitle,
                ErrorType.UpstreamServiceUnavailable,
                "TMDB did not return trending movies.");
        }

        if (response is null)
        {
            throw new MovieException(
                MovieException.ExceptionTitle,
                ErrorType.UpstreamServiceUnavailable,
                "TMDB returned an empty trending movies response.");
        }

        return response.Results.Select(TmdbMovieMapper.ToDomainModel).ToList();
    }

    public async Task<MovieSearchResponse<Movie>> SearchMoviesAsync(MovieSearchRequest request, CancellationToken cancellationToken)
    {
        TmdbPagedResponse<TmdbMovie>? response;

        try
        {
            response = await _httpClient.GetFromJsonAsync<TmdbPagedResponse<TmdbMovie>>(
                $"search/movie?query={Uri.EscapeDataString(request.query)}&page={request.page}",
                cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new MovieException(
                ExceptionTitleDetail.NotFoundTitle,
                ErrorType.EntityNotFound,
                ExceptionTitleDetail.NotFoundDetail);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new MovieException(
                MovieException.ExceptionTitle,
                ErrorType.UpstreamServiceUnavailable,
                ExceptionTitleDetail.TimeoutDetail);
        }
        catch (HttpRequestException)
        {
            throw new MovieException(
                MovieException.ExceptionTitle,
                ErrorType.UpstreamServiceUnavailable,
                ExceptionTitleDetail.ServiceUnavailableDetail);
        }

        if (response is null)
        {
            throw new MovieException(
                MovieException.ExceptionTitle,
                ErrorType.UpstreamServiceUnavailable,
                ExceptionTitleDetail.EmptyResponseDetail);
        }
        return new MovieSearchResponse<Movie>
        {
            Page = response.Page,
            Results = response.Results.Select(TmdbMovieMapper.ToDomainModel).ToList(),
            TotalPages = response.TotalPages,
            TotalResults = response.TotalResults
        };
    }

    public async Task<MovieExtendDetails> GetMovieAsync(int movieId, CancellationToken cancellationToken)
    {
        TmdbMovieExtendDetails? response;

        try
        {
            response = await _httpClient.GetFromJsonAsync<TmdbMovieExtendDetails>(
                $"movie/{movieId}", cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new MovieException(
                ExceptionTitleDetail.NotFoundTitle,
                ErrorType.EntityNotFound,
                ExceptionTitleDetail.NotFoundDetail);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new MovieException(
                MovieException.ExceptionTitle,
                ErrorType.UpstreamServiceUnavailable,
                ExceptionTitleDetail.TimeoutDetail);
        }
        catch (HttpRequestException)
        {
            throw new MovieException(
                MovieException.ExceptionTitle,
                ErrorType.UpstreamServiceUnavailable,
                ExceptionTitleDetail.ServiceUnavailableDetail);
        }

        if (response is null)
        {
            throw new MovieException(
                MovieException.ExceptionTitle,
                ErrorType.EntityNotFound,
                ExceptionTitleDetail.EmptyResponseDetail);
        }

        return response.ToDomainModel();
    }

    public async Task<IReadOnlyList<MovieTrailerVideoKeys>> GetTrailerVideoKeysAsync(int movieId, CancellationToken cancellationToken)
    {
        MovieTrailerVideoKeysResult? response;

        try
        {
            response = await _httpClient.GetFromJsonAsync<MovieTrailerVideoKeysResult>(
                $"movie/{movieId}/videos", cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new MovieException(
                ExceptionTitleDetail.NotFoundTitle,
                ErrorType.EntityNotFound,
                ExceptionTitleDetail.NotFoundDetail);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new MovieException(
                MovieException.ExceptionTitle,
                ErrorType.UpstreamServiceUnavailable,
                ExceptionTitleDetail.TimeoutDetail);
        }
        catch (HttpRequestException)
        {
            throw new MovieException(
                MovieException.ExceptionTitle,
                ErrorType.UpstreamServiceUnavailable,
                ExceptionTitleDetail.ServiceUnavailableDetail);
        }

        if (response is null)
        {
            throw new MovieException(
                MovieException.ExceptionTitle,
                ErrorType.EntityNotFound,
                ExceptionTitleDetail.EmptyResponseDetail);
        }

        return response.Results ?? [];
    }

    public async Task<TvSearchResponse<Tv>> SearchTvAsync(TvSearchRequest request, CancellationToken cancellationToken)
    {
        TmdbPagedResponse<TmdbTv>? response;

        try
        {
            var requestUri = $"search/tv?query={Uri.EscapeDataString(request.query)}&page={request.page}";
            if (request.firstAirDateYear is { } year)
            {
                requestUri += $"&first_air_date_year={year}";
            }
            response = await _httpClient.GetFromJsonAsync<TmdbPagedResponse<TmdbTv>>(
                requestUri, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new TvException(
                ExceptionTitleDetail.NotFoundTitle,
                ErrorType.EntityNotFound,
                ExceptionTitleDetail.NotFoundDetail);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TvException(
                TvException.ExceptionTitle,
                ErrorType.UpstreamServiceUnavailable,
                ExceptionTitleDetail.TimeoutDetail);
        }
        catch (HttpRequestException)
        {
            throw new TvException(
                TvException.ExceptionTitle,
                ErrorType.UpstreamServiceUnavailable,
                ExceptionTitleDetail.ServiceUnavailableDetail);
        }

        if (response is null)
        {
            throw new TvException(
                TvException.ExceptionTitle,
                ErrorType.EntityNotFound,
                ExceptionTitleDetail.EmptyResponseDetail);
        }
        return new TvSearchResponse<Tv>
        {
            Page = response.Page,
            Results = response.Results.Select(TmdbTvMapper.ToDomainModel).ToList(),
            TotalPages = response.TotalPages,
            TotalResults = response.TotalResults
        };
    }
}
