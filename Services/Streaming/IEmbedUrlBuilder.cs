namespace MoviesMafia.Services.Streaming;

/// <summary>Builds embed player URLs for the configured auto-embed provider.</summary>
public interface IEmbedUrlBuilder
{
    /// <summary>Embed URL for a full movie by TMDB id.</summary>
    string Movie(int tmdbId);

    /// <summary>Embed URL for a specific TV episode by TMDB series id, season and episode number.</summary>
    string Episode(int tmdbId, int season, int episode);
}
