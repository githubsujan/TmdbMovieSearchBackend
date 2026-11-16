namespace MovieSearchCase.WebApi.Mappers;

internal static class TvMapper
{
    public static MovieSearchCase.WebApi.Models.TvSeries.Tv ToApiModel(this MovieSearchCase.Domain.Entities.Tv tv) => new()
    {
        Id = tv.Id,
        Name = tv.Name,
        Overview = tv.Overview,
        PosterPath = tv.PosterPath,
        BackdropPath = tv.BackdropPath,
        VoteAverage = tv.VoteAverage,
        FirstAirDate = tv.FirstAirDate,
    };
}
