using System.Globalization;
using MovieSearchCase.Domain.Entities;

namespace MovieSearchCase.Infrastructure.Clients;

internal static class TmdbTvMapper
{
    public static Tv ToDomainModel(this TmdbTv tmdbTv) => new()
    {
        Id = tmdbTv.Id,
        Name = tmdbTv.Name,
        Overview = tmdbTv.Overview,
        PosterPath = tmdbTv.PosterPath,
        BackdropPath = tmdbTv.BackdropPath,
        VoteAverage = tmdbTv.VoteAverage,
        FirstAirDate = DateOnly.TryParseExact(
            tmdbTv.FirstAirDate,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var firstAirDate)
            ? firstAirDate
            : null,
    };
}
