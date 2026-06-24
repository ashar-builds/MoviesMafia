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
