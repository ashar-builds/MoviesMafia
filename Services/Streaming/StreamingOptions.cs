namespace MoviesMafia.Services.Streaming;

/// <summary>Embed/streaming settings. Bound from the "Streaming" configuration section.</summary>
public sealed class StreamingOptions
{
    public const string SectionName = "Streaming";

    /// <summary>Ordered list of embed providers offered as switchable sources on the player.</summary>
    public List<StreamingProvider> Providers { get; set; } = new();
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
