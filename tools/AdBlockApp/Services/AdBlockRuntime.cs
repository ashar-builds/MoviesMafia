using AdBlockCore;

namespace AdBlockApp.Services;

/// <summary>
/// The platform-agnostic "brain" shared by both the Windows and Android hosts: owns the
/// settings, network/cosmetic engines, and the cache/refresh lifecycle that used to live
/// directly in AdBlockShell's <c>MainWindow.xaml.cs</c>. Each platform's WebView glue
/// (<see cref="AdBlockApp.Pages.BrowserPage"/>'s platform partial) only needs to call
/// <see cref="InitializeAsync"/> once and then read <see cref="Network"/>/<see cref="Cosmetic"/>/
/// <see cref="Settings"/> per request — none of this class touches WebView2 or Android.Webkit
/// types, so it's identical on both platforms.
///
/// Registered as a singleton in <c>MauiProgram</c> so <see cref="AdBlockApp.Pages.SettingsPage"/>
/// reads/writes the SAME <see cref="Settings"/>/engines instance <see cref="AdBlockApp.Pages.BrowserPage"/>
/// is filtering with — toggling ad-block in Settings must affect the live browser immediately,
/// not a second, disconnected copy.
/// </summary>
public sealed class AdBlockRuntime
{
    private readonly string _appDataDir;
    private FilterListProvider _provider = null!;

    public AppSettings Settings { get; private set; } = null!;
    public AdBlockEngine? Network { get; private set; }
    public CosmeticEngine? Cosmetic { get; private set; }

    /// <summary>Raised whenever <see cref="Network"/>/<see cref="Cosmetic"/> are hot-swapped
    /// (initial cache seed → live build, or a background refresh) so a live WebView can
    /// re-register its cosmetic injection script against the new rule tables.</summary>
    public event Action? EnginesUpdated;

    private int _blockedRequestCount;
    private int _popupsBlockedCount;
    public int BlockedRequestCount => _blockedRequestCount;
    public int PopupsBlockedCount => _popupsBlockedCount;

    /// <summary>True once <see cref="InitializeAsync"/> has produced a usable engine pair
    /// (either from cache or a full network build) — before this, <see cref="Network"/> and
    /// <see cref="Cosmetic"/> are null and callers must not start filtering yet.</summary>
    public bool IsReady => Network is not null && Cosmetic is not null;

    public string StatusText { get; private set; } = "Loading filter lists…";
    public event Action<string>? StatusChanged;

    public AdBlockRuntime()
    {
        // FileSystem.AppDataDirectory is MAUI's cross-platform equivalent of the WPF app's
        // %LOCALAPPDATA%\AdBlockShell — resolves to app-private storage on both Windows and
        // Android with no platform-specific code needed here.
        _appDataDir = Microsoft.Maui.Storage.FileSystem.AppDataDirectory;

        // Load settings SYNCHRONOUSLY here (tiny guarded file read) so Settings is never null.
        // The platform WebView hooks read Settings.AdBlockEnabled/AllowlistedHosts the moment
        // they're wired — which now happens BEFORE InitializeAsync's engine build completes (so
        // the site can paint immediately). The engines (Network/Cosmetic) stay null until ready
        // and every hook null-checks them, so early requests simply pass through unfiltered.
        Settings = AppSettings.Load(Path.Combine(_appDataDir, "settings.json"));
    }

    private Task? _initializeTask;

    /// <summary>Idempotent: safe to call from every page that needs the runtime ready without
    /// re-downloading/re-parsing filter lists on a second call — later callers just await the
    /// same in-flight/completed task. Never throws: any failure (network, parse, disk) is caught
    /// and logged to <see cref="StatusText"/>, leaving the app usable (site loads, just
    /// unfiltered) rather than crashing — a fresh install with no cache must not be able to take
    /// the whole app down on a flaky first download.</summary>
    public Task InitializeAsync() => _initializeTask ??= InitializeCoreAsync();

    private async Task InitializeCoreAsync()
    {
        try
        {
            _provider = new FilterListProvider(Path.Combine(_appDataDir, "filters"));

            var seeded = _provider.LoadCachedEnginesIfPresent();
            if (seeded is not null)
            {
                (Network, Cosmetic) = (seeded.Network, seeded.Cosmetic);
                SetStatus($"Ad-block active (cached) · {Network.RuleCount:N0} rules · updating filters…");
                EnginesUpdated?.Invoke();
                _ = RefreshInBackgroundAsync();
            }
            else
            {
                // True first run: no cache to seed from, so we must download before filtering.
                var built = await _provider.BuildEnginesAsync(SetStatus);
                (Network, Cosmetic) = (built.Network, built.Cosmetic);
                SetStatus($"Ad-block active · {Network.RuleCount:N0} rules");
                // MUST fire so the WebView (already navigated) wires up cosmetic injection now
                // that the engines finally exist — without this, a fresh install never applies
                // cosmetic filtering until the next launch.
                EnginesUpdated?.Invoke();
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Ad-block unavailable ({ex.GetType().Name}) — site loads unfiltered.");
        }
    }

    private async Task RefreshInBackgroundAsync()
    {
        try
        {
            var built = await _provider.BuildEnginesAsync(SetStatus);
            (Network, Cosmetic) = (built.Network, built.Cosmetic);
            SetStatus($"Ad-block active · {Network.RuleCount:N0} rules");
            EnginesUpdated?.Invoke();
        }
        catch (Exception ex)
        {
            // Background refresh failed — keep serving the already-loaded cached engines; the
            // cached seed is still filtering, so this is a non-event beyond the status text.
            SetStatus($"Ad-block active (cached) · filter refresh failed ({ex.GetType().Name}).");
        }
    }

    public Task ForceRefreshAsync() => RefreshInBackgroundAsync();

    public int RecordBlockedRequest() => Interlocked.Increment(ref _blockedRequestCount);
    public int RecordBlockedPopup() => Interlocked.Increment(ref _popupsBlockedCount);

    private void SetStatus(string text)
    {
        StatusText = text;
        StatusChanged?.Invoke(text);
    }
}
