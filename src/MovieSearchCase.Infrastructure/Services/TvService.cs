using MovieSearchCase.Domain.Entities;
using MovieSearchCase.Domain.Interfaces.Clients;
using MovieSearchCase.Domain.Interfaces.Services;

namespace MovieSearchCase.Infrastructure.Services;

public class TvService : ITvService
{
    private readonly ITmdbClient _tmdbClient;

    public TvService(ITmdbClient tmdbClient)
    {
        _tmdbClient = tmdbClient;
    }

    public Task<TvSearchResponse<Tv>> SearchTvAsync(TvSearchRequest request, CancellationToken cancellationToken) =>
        _tmdbClient.SearchTvAsync(request, cancellationToken);
}
