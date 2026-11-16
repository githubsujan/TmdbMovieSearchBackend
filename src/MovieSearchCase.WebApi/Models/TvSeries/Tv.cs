namespace MovieSearchCase.WebApi.Models.TvSeries;
public record Tv
{
    public required int Id { get; init; }

    public required string Name { get; init; }

    public string? Overview { get; init; }

    public string? PosterPath { get; init; }

    public string? BackdropPath { get; init; }

    public double VoteAverage { get; init; }

    public DateOnly? FirstAirDate { get; init; }
}