namespace MoviesMafia.Services.Storage;

/// <summary>File-storage settings. Bound from the "Storage" configuration section.</summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>
    /// Absolute or content-root-relative folder where avatars are written.
    /// Defaults to "App_Data/avatars" so images survive outside wwwroot and are served via a dedicated endpoint.
    /// </summary>
    public string AvatarsPath { get; set; } = "App_Data/avatars";

    /// <summary>Public request path prefix used to serve avatars.</summary>
    public string AvatarsRequestPath { get; set; } = "/avatars";
}
