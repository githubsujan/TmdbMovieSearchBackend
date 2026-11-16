using System.ComponentModel.DataAnnotations;

namespace MovieSearchCase.Infrastructure.Caching;

public sealed class CachingOptions
{
    public const string SectionName = "Caching";

    private const int MaxMinutes = 10_080;

    public bool Enabled { get; init; } = true;

    [Range(1, MaxMinutes)]
    public int SearchMinutes { get; init; } = 10;

    [Range(1, MaxMinutes)]
    public int TrendingMinutes { get; init; } = 30;

    [Range(1, MaxMinutes)]
    public int MovieDetailsMinutes { get; init; } = 360;

    [Range(1, MaxMinutes)]
    public int TrailersMinutes { get; init; } = 1_440;

    [Range(1, 200)]
    public int MaxQueryLength { get; init; } = 100;
}