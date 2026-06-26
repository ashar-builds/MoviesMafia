namespace MoviesMafia.Services.Streaming;

/// <summary>Builds embed player URLs for every configured streaming provider.</summary>
public interface IEmbedUrlBuilder
{
    /// <summary>Embed sources for a full movie by TMDB id, one per configured provider.</summary>
    IReadOnlyList<EmbedSource> Movie(int tmdbId);

    /// <summary>Embed sources for a specific TV episode by TMDB series id, season and episode number.</summary>
    IReadOnlyList<EmbedSource> Episode(int tmdbId, int season, int episode);
}

/// <summary>A named, ready-to-embed player URL for one provider.</summary>
public sealed record EmbedSource(string Name, string Url);
