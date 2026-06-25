namespace MoviesMafia.Services.Tmdb.Dtos;

/// <summary>A single TV series from a TMDB discover/search response.</summary>
public sealed class SeriesResult
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? OriginalName { get; set; }
    public string? Overview { get; set; }
    public string? PosterPath { get; set; }
    public string? BackdropPath { get; set; }
    public string? FirstAirDate { get; set; }
    public double VoteAverage { get; set; }
    public int VoteCount { get; set; }
    public double Popularity { get; set; }
}

/// <summary>A paged TMDB series list response.</summary>
public sealed class SeriesPage
{
    public int Page { get; set; }
    public List<SeriesResult> Results { get; set; } = [];
    public int TotalPages { get; set; }
    public int TotalResults { get; set; }
}

/// <summary>Detailed TMDB series info, including the season list used by the episode picker.</summary>
public sealed class SeriesDetails
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? OriginalName { get; set; }
    public string? Tagline { get; set; }
    public string? Overview { get; set; }
    public string? PosterPath { get; set; }
    public string? BackdropPath { get; set; }
    public string? FirstAirDate { get; set; }
    public string? LastAirDate { get; set; }
    public string? Status { get; set; }
    public string? Homepage { get; set; }
    public string? Type { get; set; }
    public string? OriginalLanguage { get; set; }
    public double Popularity { get; set; }
    public bool InProduction { get; set; }
    public bool Adult { get; set; }
    public double VoteAverage { get; set; }
    public int VoteCount { get; set; }
    public int? NumberOfSeasons { get; set; }
    public int? NumberOfEpisodes { get; set; }
    public List<int> EpisodeRunTime { get; set; } = [];
    public List<SeriesGenre> Genres { get; set; } = [];
    public List<SeriesSeason> Seasons { get; set; } = [];
    public List<SeriesNetwork> Networks { get; set; } = [];
    public List<SeriesCreator> CreatedBy { get; set; } = [];
    public List<string> OriginCountry { get; set; } = [];
}

public sealed class SeriesCreator
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? ProfilePath { get; set; }
}

public sealed class SeriesNetwork
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? LogoPath { get; set; }
    public string? OriginCountry { get; set; }
}

public sealed class SeriesGenre
{
    public int Id { get; set; }
    public string? Name { get; set; }
}

public sealed class SeriesSeason
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Overview { get; set; }
    public string? PosterPath { get; set; }
    public string? AirDate { get; set; }
    public int EpisodeCount { get; set; }
    public int SeasonNumber { get; set; }
}
