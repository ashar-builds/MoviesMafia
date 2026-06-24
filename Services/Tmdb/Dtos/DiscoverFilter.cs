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
}
