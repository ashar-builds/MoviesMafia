namespace MoviesMafia.Services.Streaming;

/// <summary>Embed/streaming settings. Bound from the "Streaming" configuration section.</summary>
public sealed class StreamingOptions
{
    public const string SectionName = "Streaming";

    /// <summary>
    /// Ordered list of embed providers offered as switchable sources on the player. Used as the
    /// fallback when <see cref="ProvidersUrl"/> is unset or its first fetch hasn't succeeded yet.
    /// </summary>
    public List<StreamingProvider> Providers { get; set; } = new();

    /// <summary>
    /// Optional URL (e.g. a GitHub Gist *raw* URL) returning a JSON array of providers. When set,
    /// the app loads the provider list from here and refreshes it in the background, so providers
    /// can be added/removed without a redeploy. Falls back to <see cref="Providers"/> on failure.
    /// </summary>
    public string? ProvidersUrl { get; set; }

    /// <summary>How often to re-fetch <see cref="ProvidersUrl"/>, in minutes. Default 30.</summary>
    public int ProvidersRefreshMinutes { get; set; } = 30;
}

/// <summary>
/// A single embed provider. Templates use the placeholders <c>{id}</c>, <c>{season}</c> and
/// <c>{episode}</c>, letting each provider declare its own URL shape (path or query style).
/// </summary>
public sealed class StreamingProvider
{
    /// <summary>Label shown on the source-switcher button.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Movie embed URL template, e.g. <c>https://host/movie/{id}</c>.</summary>
    public string MovieTemplate { get; set; } = string.Empty;

    /// <summary>Episode embed URL template, e.g. <c>https://host/tv/{id}/{season}/{episode}</c>.</summary>
    public string EpisodeTemplate { get; set; } = string.Empty;
}
