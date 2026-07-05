namespace MoviesMafia.Services.Tmdb.Dtos;

/// <summary>
/// Optional filters for the TMDB <c>discover</c> endpoints. All members are nullable;
/// a null value means "don't constrain on this axis". <see cref="TmdbClient"/> maps these
/// onto the right query parameters for movies vs. series (the date param differs).
/// </summary>
public sealed record DiscoverFilter
{
    /// <summary>TMDB sort spec, e.g. "popularity.desc", "vote_average.desc", "primary_release_date.desc".</summary>
    public string SortBy { get; init; } = "popularity.desc";

    /// <summary>Restrict to a single release/first-air year.</summary>
    public int? Year { get; init; }

    /// <summary>Minimum TMDB vote average (0–10).</summary>
    public double? MinRating { get; init; }

    /// <summary>
    /// Minimum vote count. Defaults to a sane floor so a single 10/10 vote doesn't dominate
    /// rating-sorted results — TMDB's own "top rated" lists apply a similar guard.
    /// </summary>
    public int MinVotes { get; init; } = 50;

    // ---- Collection axes (Bollywood / Hollywood / Marvel / DC / Anime …) ----
    // Each is a raw TMDB query value. TMDB treats comma as OR and pipe (|) as AND
    // within a single param; callers build those strings. Null = don't constrain.

    /// <summary>TMDB genre id(s), e.g. "16" (Animation) or "28,12" (Action OR Adventure).</summary>
    public string? WithGenres { get; init; }

    /// <summary>TMDB production-company id(s), e.g. "420" (Marvel Studios).</summary>
    public string? WithCompanies { get; init; }

    /// <summary>TMDB keyword id(s), e.g. "9715" (superhero).</summary>
    public string? WithKeywords { get; init; }

    /// <summary>ISO 639-1 original language, e.g. "hi" (Hindi), "en", "ja", "ko".</summary>
    public string? WithOriginalLanguage { get; init; }

    /// <summary>ISO 3166-1 origin country, e.g. "JP", "KR", "IN" (TV discover).</summary>
    public string? WithOriginCountry { get; init; }

    /// <summary>ISO 3166-1 region for release filtering, e.g. "IN", "US" (movie discover).</summary>
    public string? Region { get; init; }
}
