using System.Globalization;
using MovieSearchCase.Domain.Entities;

namespace MovieSearchCase.Infrastructure.Clients;

internal static class TmdbMovieMapper
{
    public static Movie ToDomainModel(this TmdbMovie tmdbMovie) => new()
    {
        Id = tmdbMovie.Id,
        Title = tmdbMovie.Title,
        Overview = tmdbMovie.Overview,
        PosterPath = tmdbMovie.PosterPath,
        BackdropPath = tmdbMovie.BackdropPath,
        VoteAverage = tmdbMovie.VoteAverage,
        ReleaseDate = DateOnly.TryParseExact(
            tmdbMovie.ReleaseDate,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var releaseDate)
            ? releaseDate
            : null,
    };

    public static MovieExtendDetails ToDomainModel(this TmdbMovieExtendDetails tmdbMovieExtendDetails) => new()
    {
        Id = tmdbMovieExtendDetails.Id,
        Title = tmdbMovieExtendDetails.Title,
        Overview = tmdbMovieExtendDetails.Overview,
        PosterPath = tmdbMovieExtendDetails.PosterPath,
        BackdropPath = tmdbMovieExtendDetails.BackdropPath,
        VoteAverage = tmdbMovieExtendDetails.VoteAverage,
        ReleaseDate = DateOnly.TryParseExact(
            tmdbMovieExtendDetails.ReleaseDate,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var releaseDate)
            ? releaseDate
            : null,
        Runtime = tmdbMovieExtendDetails.Runtime,
        Tagline = tmdbMovieExtendDetails.Tagline,
        Genres = tmdbMovieExtendDetails.Genres
    };
}
