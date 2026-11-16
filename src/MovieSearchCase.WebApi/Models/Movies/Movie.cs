using MovieSearchCase.Domain.Entities;

namespace MovieSearchCase.WebApi.Models.Movies;

/// <summary>
/// API response shape for a movie. Deliberately separate from
/// <see cref="Domain.Entities.Movie"/> so the wire format can evolve independently
/// of the domain model.
/// </summary>
public record Movie
{
    public required int Id { get; init; }

    public required string Title { get; init; }

    public string? Overview { get; init; }

    public string? PosterPath { get; init; }

    public string? BackdropPath { get; init; }

    public double VoteAverage { get; init; }

    public DateOnly? ReleaseDate { get; init; }
}

public record MovieExtendDetails : Movie
{
    public int? Runtime { get; init; }

    public string? Tagline { get; init; }

    public List<Genre>? Genres { get; init; }
}

public record MovieTrailerVideoKeys
{
    public string? Name { get; init; }
    public string? Key { get; init; }
    public string? Site { get; init; }
    public string? Type { get; init; }
}
