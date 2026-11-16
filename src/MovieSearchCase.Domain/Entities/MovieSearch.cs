namespace MovieSearchCase.Domain.Entities;

public record MovieSearchRequest(string query, int page);
public record MovieSearchResponse<T>
{
    public int Page { get; init; }
    public int TotalPages { get; init; }
    public int TotalResults { get; init; }
    public required IReadOnlyList<T> Results { get; init; }
}
