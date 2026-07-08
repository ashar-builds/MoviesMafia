using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AdBlockCore;

/// <summary>
/// Source-generated (reflection-free) JSON metadata for <see cref="AppSettings"/>. This is what
/// lets the Android Release build keep the linker ON: reflection-based serialization forces
/// <c>AndroidLinkMode=None</c> (huge APK, slow startup) because the trimmer can't see which BCL
/// members the reflection paths need; a compile-time context has no such blind spot, so trimming
/// is safe and the linker/marshal-method defaults stay in effect. See AdBlockApp.csproj.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class AppSettingsJsonContext : JsonSerializerContext;

/// <summary>
/// Persisted user preferences: the master ad-block toggle and a per-site allowlist. Plain JSON
/// file I/O with no WebView2/WPF dependency — the host (MainWindow) decides WHERE the file
/// lives (<c>%LOCALAPPDATA%\AdBlockShell\settings.json</c>) and calls <see cref="Save"/> after
/// mutating; this class only owns the shape and the load/save mechanics.
/// </summary>
public sealed class AppSettings
{
    public bool AdBlockEnabled { get; set; } = true;

    /// <summary>Hosts (and their subdomains) exempted from BOTH network and cosmetic
    /// filtering — for a provider whose playback breaks under blocking.</summary>
    public List<string> AllowlistedHosts { get; set; } = new();

    /// <summary>Master switch for the background GitHub-Releases update check (see
    /// <see cref="UpdateChecker"/>). Default on — an unsigned, non-store-distributed app is the
    /// case where staying current matters most, but the user can turn it off from Settings.</summary>
    public bool AutoUpdateEnabled { get; set; } = true;

    /// <summary>True once the first-run explainer screen has been shown and dismissed — set once
    /// and never reset, so re-launching the app never re-shows it.</summary>
    public bool FirstRunCompleted { get; set; }

    private string? _path;

    public static AppSettings Load(string path)
    {
        AppSettings settings;
        try
        {
            settings = File.Exists(path)
                ? JsonSerializer.Deserialize(File.ReadAllText(path), AppSettingsJsonContext.Default.AppSettings) ?? new AppSettings()
                : new AppSettings();
        }
        catch
        {
            // Corrupt/unreadable settings file — start fresh rather than crash the app over prefs.
            settings = new AppSettings();
        }
        settings._path = path;
        return settings;
    }

    public void Save()
    {
        if (_path is null) return;
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(this, AppSettingsJsonContext.Default.AppSettings));
    }

    /// <summary>True if <paramref name="host"/> is allowlisted directly or is a subdomain of an
    /// allowlisted host.</summary>
    public bool IsAllowlisted(string host)
    {
        host = host.ToLowerInvariant();
        foreach (var entry in AllowlistedHosts)
        {
            var h = entry.ToLowerInvariant();
            if (host == h || host.EndsWith("." + h, StringComparison.Ordinal)) return true;
        }
        return false;
    }

    public void ToggleAllowlist(string host)
    {
        host = host.ToLowerInvariant();
        int removed = AllowlistedHosts.RemoveAll(h => h.Equals(host, StringComparison.OrdinalIgnoreCase));
        if (removed == 0) AllowlistedHosts.Add(host);
        Save();
    }
}
