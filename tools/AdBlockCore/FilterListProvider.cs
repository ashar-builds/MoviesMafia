using System.IO;
using System.Net.Http;

namespace AdBlockCore;

/// <summary>The two engines built from one pass over the filter lists — see
/// <see cref="FilterListProvider.BuildEnginesAsync"/>. uBO's own lists interleave network and
/// cosmetic rules in the same files, so both engines are populated from the same downloaded
/// text rather than needing separate list sources.</summary>
public sealed record FilterEngines(AdBlockEngine Network, CosmeticEngine Cosmetic);

/// <summary>
/// Downloads the filter lists uBlock Origin ships with and caches them on disk so we only
/// hit the network when the cache is stale. These are the exact lists uBO enables by
/// default, so the PoC blocks with the same rule set.
/// </summary>
public sealed class FilterListProvider
{
    // uBlock Origin's default network lists (raw text, ABP syntax).
    private static readonly (string Name, string Url)[] Lists =
    {
        // uBO's own filters (the core of what makes uBO effective)
        ("ubo-filters",  "https://ublockorigin.github.io/uAssets/filters/filters.min.txt"),
        ("ubo-badware",  "https://ublockorigin.github.io/uAssets/filters/badware.txt"),
        ("ubo-privacy",  "https://ublockorigin.github.io/uAssets/filters/privacy.min.txt"),
        ("ubo-quick",    "https://ublockorigin.github.io/uAssets/filters/quick-fixes.txt"),
        // The community staples uBO enables by default
        ("easylist",     "https://easylist.to/easylist/easylist.txt"),
        ("easyprivacy",  "https://easylist.to/easylist/easyprivacy.txt"),
        // Popup/annoyance coverage — most relevant to the streaming-provider ads
        ("ubo-annoyances", "https://ublockorigin.github.io/uAssets/filters/annoyances.txt"),
    };

    private readonly string _cacheDir;
    private readonly TimeSpan _maxAge;
    private readonly HttpClient _http;

    public FilterListProvider(string cacheDir, TimeSpan? maxAge = null)
    {
        _cacheDir = cacheDir;
        _maxAge = maxAge ?? TimeSpan.FromDays(4);
        Directory.CreateDirectory(_cacheDir);
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("AdBlockShell-PoC/1.0");
    }

    /// <summary>
    /// Builds engines purely from whatever is already on disk — no network calls, so this
    /// returns near-instantly. Used to seed filtering on startup without blocking first paint on
    /// a filter-list download (see HANDOFF's "Filter-list refresh UX" task); the caller should
    /// follow up with <see cref="BuildEnginesAsync"/> in the background to get fresh lists and
    /// PSL-correct third-party detection. Returns null if any list has never been downloaded —
    /// on a true first run there's nothing to seed from, so the caller must await the full
    /// <see cref="BuildEnginesAsync"/> instead.
    /// </summary>
    public FilterEngines? LoadCachedEnginesIfPresent()
    {
        var network = new AdBlockEngine();
        var cosmetic = new CosmeticEngine();

        foreach (var (name, _) in Lists)
        {
            var path = Path.Combine(_cacheDir, name + ".txt");
            if (!File.Exists(path)) return null;

            var text = File.ReadAllText(path);
            network.LoadFilterList(text);
            cosmetic.LoadFilterList(text);
        }

        // No PSL classifier yet — AdBlockEngine falls back to the naive last-two-labels
        // comparison until BuildEnginesAsync's background refresh calls SetSiteClassifier.
        return new FilterEngines(network, cosmetic);
    }

    /// <summary>
    /// Ensure every list is present locally (downloading stale/missing ones), then feed each
    /// line to BOTH the network engine (<see cref="AbpRule"/>) and the cosmetic engine
    /// (<see cref="CosmeticRule"/>) — uBO's own lists mix both rule kinds in the same files, and
    /// each parser already ignores lines outside its syntax. Falls back to any cached copy if a
    /// download fails, so the app still works offline once seeded. Also builds the Public Suffix
    /// List classifier used for PSL-correct third-party detection and wires it into the network engine.
    /// </summary>
    public async Task<FilterEngines> BuildEnginesAsync(Action<string>? log = null)
    {
        var network = new AdBlockEngine();
        var cosmetic = new CosmeticEngine();

        var pslCacheDir = Path.Combine(_cacheDir, "psl");
        var classifier = await PublicSuffixClassifier.CreateAsync(pslCacheDir, _maxAge, log);
        network.SetSiteClassifier(classifier);

        foreach (var (name, url) in Lists)
        {
            var path = Path.Combine(_cacheDir, name + ".txt");
            var fresh = File.Exists(path) &&
                        (DateTimeNow() - File.GetLastWriteTimeUtc(path)) < _maxAge;

            if (!fresh)
            {
                try
                {
                    log?.Invoke($"Downloading {name}…");
                    var text = await _http.GetStringAsync(url);
                    await File.WriteAllTextAsync(path, text);
                }
                catch (Exception ex)
                {
                    log?.Invoke($"  {name} download failed ({ex.Message}); using cache if present.");
                }
            }

            if (File.Exists(path))
            {
                var text = await File.ReadAllTextAsync(path);
                network.LoadFilterList(text);
                cosmetic.LoadFilterList(text);
                log?.Invoke($"Loaded {name} ({network.RuleCount} network / {cosmetic.RuleCount} cosmetic rules so far).");
            }
            else
            {
                log?.Invoke($"  {name} unavailable — skipped.");
            }
        }

        log?.Invoke($"Engine ready: {network.RuleCount} network rules ({network.SkippedLines} unsupported lines skipped), " +
                    $"{cosmetic.RuleCount} cosmetic rules ({cosmetic.SkippedUnsupported} procedural/scriptlet rules skipped).");
        return new FilterEngines(network, cosmetic);
    }

    // Isolated so the rest of the code stays clock-call-free where it matters.
    private static DateTime DateTimeNow() => DateTime.UtcNow;
}
