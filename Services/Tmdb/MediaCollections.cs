using MoviesMafia.Services.Tmdb.Dtos;

namespace MoviesMafia.Services.Tmdb;

/// <summary>
/// A curated "collection" — an industry (Bollywood/Hollywood), studio (Marvel/DC/Pixar),
/// or category (Anime/K-Drama) — that maps to a set of TMDB discover parameters.
/// </summary>
/// <param name="Id">Stable slug used as the reactive filter value (kebab-case).</param>
/// <param name="Label">Display name shown on the pill.</param>
/// <param name="Emoji">Small glyph for the pill (purely decorative).</param>
public sealed record MediaCollection(string Id, string Label, string Emoji)
{
    /// <summary>TMDB genre id(s) — comma = OR. Genre 16 (Animation) is valid on both movie &amp; TV.</summary>
    public string? Genres { get; init; }

    /// <summary>
    /// TMDB production-company id(s). NOTE: for <c>with_companies</c>, comma means AND and
    /// pipe (|) means OR — so a studio that spans several company ids uses pipes to union them.
    /// </summary>
    public string? Companies { get; init; }

    /// <summary>TMDB keyword id(s) — comma = OR.</summary>
    public string? Keywords { get; init; }

    /// <summary>ISO 639-1 original language, e.g. "hi", "en", "ja", "ko".</summary>
    public string? OriginalLanguage { get; init; }

    /// <summary>ISO 3166-1 origin country (TV discover), e.g. "JP", "KR", "IN".</summary>
    public string? OriginCountry { get; init; }

    /// <summary>Merge this collection's axes onto an existing filter (sort/year/rating are preserved).</summary>
    public DiscoverFilter Apply(DiscoverFilter baseFilter) => baseFilter with
    {
        WithGenres = Genres,
        WithCompanies = Companies,
        WithKeywords = Keywords,
        WithOriginalLanguage = OriginalLanguage,
        WithOriginCountry = OriginCountry,
    };
}

/// <summary>
/// The catalog of collections offered on the Discover page. IDs are stable TMDB ids:
/// company ids (Marvel Studios 420, DC 9993/429, Pixar 3, Ghibli 10342, Disney 2,
/// DreamWorks 521), genre 16 = Animation, keyword 9715 = superhero.
/// </summary>
public static class MediaCollections
{
    public static readonly IReadOnlyList<MediaCollection> All =
    [
        // ---- Industries (language / origin based) ----
        new("hollywood", "Hollywood", "🎬") { OriginalLanguage = "en" },
        new("bollywood", "Bollywood", "🇮🇳") { OriginalLanguage = "hi" },
        new("korean", "Korean", "🇰🇷") { OriginalLanguage = "ko" },
        new("anime", "Anime", "🎌") { Genres = "16", OriginalLanguage = "ja" },

        // ---- Studios / franchises (company based; pipe = OR across a studio's company ids) ----
        // Verified against TMDB discover: Marvel 420|7505|19551 ≈ 138, DC 429|9993|128064 ≈ 176.
        new("marvel", "Marvel", "🦸") { Companies = "420|7505|19551" },
        new("dc", "DC", "🦇") { Companies = "429|9993|128064" },
        new("pixar", "Pixar", "💡") { Companies = "3" },
        new("ghibli", "Studio Ghibli", "🌱") { Companies = "10342" },
        new("disney", "Disney", "🏰") { Companies = "2" },
        new("dreamworks", "DreamWorks", "🌙") { Companies = "521" },
    ];

    public static MediaCollection? Find(string? id) =>
        string.IsNullOrWhiteSpace(id) ? null : All.FirstOrDefault(c => c.Id == id);
}
