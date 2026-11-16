namespace MovieSearchCase.WebApi.Mappers;

internal static class MovieMapper
{
    public static Models.Movies.Movie ToApiModel(this Domain.Entities.Movie movie) => new()
    {
        Id = movie.Id,
        Title = movie.Title,
        Overview = movie.Overview,
        PosterPath = movie.PosterPath,
        BackdropPath = movie.BackdropPath,
        VoteAverage = movie.VoteAverage,
        ReleaseDate = movie.ReleaseDate,
    };

     public static Models.Movies.MovieExtendDetails ToApiModel(this Domain.Entities.MovieExtendDetails movieExtendDetails) => new()
    {
        Id = movieExtendDetails.Id,
        Title = movieExtendDetails.Title,
        Overview = movieExtendDetails.Overview,
        PosterPath = movieExtendDetails.PosterPath,
        BackdropPath = movieExtendDetails.BackdropPath,
        VoteAverage = movieExtendDetails.VoteAverage,
        ReleaseDate = movieExtendDetails.ReleaseDate,
        Runtime = movieExtendDetails.Runtime,
        Tagline = movieExtendDetails.Tagline,
        Genres = movieExtendDetails.Genres
    };

    public static Models.Movies.MovieTrailerVideoKeys ToApiModel(this Domain.Entities.MovieTrailerVideoKeys movieTrailerVideoKeys) => new()
    {
        Name = movieTrailerVideoKeys.Name,
        Key = movieTrailerVideoKeys.Key,
        Site = movieTrailerVideoKeys.Site,
        Type = movieTrailerVideoKeys.Type
    };
}
