using System.ComponentModel.DataAnnotations;

namespace MoviesMafia.Services.Tmdb;

/// <summary>Configuration for the TMDB API client. Bound from the "Tmdb" configuration section.</summary>
public sealed class TmdbOptions
{
    public const string SectionName = "Tmdb";

    [Required]
    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.themoviedb.org/3/";

    /// <summary>Base URL for poster/backdrop images. A TMDB size segment is appended per request.</summary>
    public string ImageBaseUrl { get; set; } = "https://image.tmdb.org/t/p/";
}
