namespace MoviesMafia.Services.Tmdb.Dtos;

/// <summary>A single movie from a TMDB discover/search response.</summary>
public sealed class MovieResult
{
    public int Id { get; set; }
    public string? Title { get; set; }
    public string? OriginalTitle { get; set; }
    public string? Overview { get; set; }
    public string? PosterPath { get; set; }
    public string? BackdropPath { get; set; }
    public string? ReleaseDate { get; set; }
    public double VoteAverage { get; set; }
    public int VoteCount { get; set; }
    public double Popularity { get; set; }
}

/// <summary>A paged TMDB movie list response.</summary>
public sealed class MoviePage
{
    public int Page { get; set; }
    public List<MovieResult> Results { get; set; } = [];
    public int TotalPages { get; set; }
    public int TotalResults { get; set; }
}

/// <summary>Detailed TMDB movie info used by the details page.</summary>
public sealed class MovieDetails
{
    public int Id { get; set; }
    public string? Title { get; set; }
    public string? OriginalTitle { get; set; }
    public string? Tagline { get; set; }
    public string? Overview { get; set; }
    public string? PosterPath { get; set; }
    public string? BackdropPath { get; set; }
    public string? ReleaseDate { get; set; }
    public int? Runtime { get; set; }
    public double VoteAverage { get; set; }
    public int VoteCount { get; set; }
    public string? Status { get; set; }
    public string? Homepage { get; set; }
    public string? ImdbId { get; set; }
    public string? OriginalLanguage { get; set; }
    public double Popularity { get; set; }
    public long Budget { get; set; }
    public long Revenue { get; set; }
    public bool Adult { get; set; }
    public List<MediaGenre> Genres { get; set; } = [];
    public List<ProductionCompany> ProductionCompanies { get; set; } = [];
    public List<ProductionCountry> ProductionCountries { get; set; } = [];
    public List<SpokenLanguage> SpokenLanguages { get; set; } = [];
}

public sealed class MediaGenre
{
    public int Id { get; set; }
    public string? Name { get; set; }
}

public sealed class ProductionCompany
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? LogoPath { get; set; }
    public string? OriginCountry { get; set; }
}

public sealed class ProductionCountry
{
    public string? Name { get; set; }
}

public sealed class SpokenLanguage
{
    public string? Name { get; set; }
    public string? EnglishName { get; set; }
}

/// <summary>TMDB credits (cast + crew) response.</summary>
public sealed class CreditsResponse
{
    public int Id { get; set; }
    public List<CastMember> Cast { get; set; } = [];
    public List<CrewMember> Crew { get; set; } = [];
}

public sealed class CastMember
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Character { get; set; }
    public string? ProfilePath { get; set; }
    public int Order { get; set; }
}

public sealed class CrewMember
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Job { get; set; }
    public string? Department { get; set; }
    public string? ProfilePath { get; set; }
}
