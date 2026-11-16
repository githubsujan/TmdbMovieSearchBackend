using System.Text.Json.Serialization;

namespace MovieSearchCase.Domain.Entities;
public record MovieTrailerVideoKeys
{
    public required string Name { get; init; }
    public required string Key { get; init; }
    public required string Site { get; init; }
    public required string Type { get; init; }
}

public record MovieTrailerVideoKeysResult
{
    public int Id {get;init;}
    public IReadOnlyList<MovieTrailerVideoKeys>? Results {get;set;}
}