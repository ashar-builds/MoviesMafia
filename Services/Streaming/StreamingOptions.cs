namespace MoviesMafia.Services.Streaming;

/// <summary>Embed/streaming settings. Bound from the "Streaming" configuration section.</summary>
public sealed class StreamingOptions
{
    public const string SectionName = "Streaming";

    /// <summary>Base URL of the third-party auto-embed provider (no trailing slash required).</summary>
    public string AutoEmbedUrl { get; set; } = string.Empty;
}
