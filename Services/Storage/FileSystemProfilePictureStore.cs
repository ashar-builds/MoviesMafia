using Microsoft.Extensions.Options;

namespace MoviesMafia.Services.Storage;

/// <summary>
/// Stores avatars on the local file system under <see cref="StorageOptions.AvatarsPath"/>.
/// Only the file name is persisted in the database; images are served from a dedicated request path.
/// </summary>
public sealed class FileSystemProfilePictureStore : IProfilePictureStore
{
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

    private readonly StorageOptions _options;
    private readonly string _root;

    public FileSystemProfilePictureStore(IOptions<StorageOptions> options, IHostEnvironment env)
    {
        _options = options.Value;
        _root = Path.IsPathRooted(_options.AvatarsPath)
            ? _options.AvatarsPath
            : Path.Combine(env.ContentRootPath, _options.AvatarsPath);
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(string userName, Stream content, string originalFileName, CancellationToken ct = default)
    {
        var extension = Path.GetExtension(originalFileName);
        if (!AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException($"Unsupported image type '{extension}'.");
        }

        // Stable, per-user file name keeps storage tidy and avoids orphaned files on re-upload.
        var fileName = $"{Sanitize(userName)}{extension.ToLowerInvariant()}";

        // Remove any previous avatar for this user (possibly a different extension).
        RemoveExisting(userName);

        var fullPath = Path.Combine(_root, fileName);
        await using (var file = File.Create(fullPath))
        {
            await content.CopyToAsync(file, ct);
        }

        return fileName;
    }

    public Task DeleteAsync(string fileName, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            var fullPath = Path.Combine(_root, Path.GetFileName(fileName));
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }

        return Task.CompletedTask;
    }

    public string PublicPath(string? fileName) =>
        string.IsNullOrWhiteSpace(fileName)
            ? "/images/avatar-placeholder.svg"
            : $"{_options.AvatarsRequestPath.TrimEnd('/')}/{fileName}";

    private void RemoveExisting(string userName)
    {
        foreach (var existing in Directory.GetFiles(_root, $"{Sanitize(userName)}.*"))
        {
            File.Delete(existing);
        }
    }

    private static string Sanitize(string userName)
    {
        var cleaned = new string(userName.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());
        return string.IsNullOrEmpty(cleaned) ? "user" : cleaned.ToLowerInvariant();
    }
}
