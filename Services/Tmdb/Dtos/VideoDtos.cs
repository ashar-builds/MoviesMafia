namespace MoviesMafia.Services.Tmdb.Dtos;

/// <summary>A single video (trailer/teaser/clip) attached to a movie or series.</summary>
public sealed class VideoResult
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Key { get; set; }
    public string? Site { get; set; }
    public string? Type { get; set; }
    public bool Official { get; set; }

    /// <summary>True when this video is a YouTube-hosted trailer or teaser that can be embedded.</summary>
    public bool IsYouTubeTrailer =>
        string.Equals(Site, "YouTube", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(Key);
}

/// <summary>The TMDB videos response for a movie or series.</summary>
public sealed class VideoPage
{
    public int Id { get; set; }
    public List<VideoResult> Results { get; set; } = [];
}
