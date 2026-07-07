using System.Net.Http;
using System.Text.Json;

namespace AdBlockCore;

/// <summary>One GitHub Releases API asset entry — just the two fields callers need to pick
/// the right download (Windows installer/exe vs. Android APK).</summary>
public sealed record ReleaseAsset(string Name, string BrowserDownloadUrl);

/// <summary>The subset of GitHub's release JSON both platforms need: the version tag to
/// compare, a human link for "what's new", and the downloadable assets.</summary>
public sealed record ReleaseInfo(string TagName, string HtmlUrl, string Body, IReadOnlyList<ReleaseAsset> Assets);

/// <summary>
/// Portable (no WPF/WebView2/MAUI/Android types) check against GitHub's public Releases API —
/// see HANDOFF.md Task 3. Both hosts share this for the "is there a newer version" question;
/// only the "how do I install it" step is platform-specific (Windows: download+relaunch the
/// installer; Android: hand the user to the system package installer — see each platform's
/// BrowserPage/SettingsPage partial).
///
/// Never throws: a failed check (offline, rate-limited, malformed response) must not crash or
/// block app startup — every failure path returns null and callers treat that as "no update
/// info available right now," not an error to surface.
/// </summary>
public sealed class UpdateChecker
{
    private readonly HttpClient _http;
    private readonly string _releasesApiUrl;

    public UpdateChecker(string owner = "im-ashar", string repo = "MoviesMafia", HttpClient? http = null)
    {
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("AdBlockApp-UpdateChecker/1.0");
        if (_http.DefaultRequestHeaders.Accept.Count == 0)
            _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        _releasesApiUrl = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
    }

    public async Task<ReleaseInfo?> TryGetLatestReleaseAsync()
    {
        try
        {
            using var response = await _http.GetAsync(_releasesApiUrl);
            if (!response.IsSuccessStatusCode) return null; // 404 (no releases yet), 403 (rate limit), etc.

            using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);
            var root = doc.RootElement;

            var tag = root.TryGetProperty("tag_name", out var tagEl) ? tagEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(tag)) return null;

            var htmlUrl = root.TryGetProperty("html_url", out var urlEl) ? urlEl.GetString() ?? "" : "";
            var body = root.TryGetProperty("body", out var bodyEl) ? bodyEl.GetString() ?? "" : "";

            var assets = new List<ReleaseAsset>();
            if (root.TryGetProperty("assets", out var assetsEl) && assetsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assetsEl.EnumerateArray())
                {
                    var name = asset.TryGetProperty("name", out var n) ? n.GetString() : null;
                    var url = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
                    if (name is not null && url is not null) assets.Add(new ReleaseAsset(name, url));
                }
            }

            return new ReleaseInfo(tag, htmlUrl, body, assets);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>True if <paramref name="releaseTag"/> (e.g. "v1.4.0" or "1.4.0") is strictly
    /// newer than <paramref name="currentVersion"/> (e.g. MAUI's <c>AppInfo.VersionString</c>,
    /// "1.4"). Unparseable input is treated as "not newer" — we never prompt an update we can't
    /// confidently compare.</summary>
    public static bool IsNewer(string releaseTag, string currentVersion)
    {
        var release = ParseVersion(releaseTag);
        var current = ParseVersion(currentVersion);
        return release is not null && current is not null && release > current;
    }

    private static Version? ParseVersion(string raw)
    {
        var trimmed = raw.Trim().TrimStart('v', 'V');
        return Version.TryParse(trimmed, out var v) ? v : null;
    }
}
