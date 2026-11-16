namespace MovieSearchCase.Domain.Entities;

public record TvSearchRequest(string query, int page, int? firstAirDateYear);
public record TvSearchResponse<T>
{
    public int Page { get; init; }
    public int TotalPages { get; init; }
    public int TotalResults { get; init; }
    public required IReadOnlyList<T> Results { get; init; }
}
