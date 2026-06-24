namespace MoviesMafia.Services.Storage;

/// <summary>Stores and serves user avatar images, keyed by a stable file name.</summary>
public interface IProfilePictureStore
{
    /// <summary>
    /// Persists <paramref name="content"/> as the avatar for <paramref name="userName"/>, replacing any existing
    /// avatar for that user, and returns the stored file name (e.g. "alice.png").
    /// </summary>
    Task<string> SaveAsync(string userName, Stream content, string originalFileName, CancellationToken ct = default);

    /// <summary>Deletes the stored avatar with the given file name, if present.</summary>
    Task DeleteAsync(string fileName, CancellationToken ct = default);

    /// <summary>The public request path used to serve a stored avatar (e.g. "/avatars/alice.png").</summary>
    string PublicPath(string? fileName);
}
