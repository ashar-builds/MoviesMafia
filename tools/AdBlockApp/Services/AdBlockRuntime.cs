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
    }

    private Task? _initializeTask;

    /// <summary>Idempotent: safe to call from every page that needs the runtime ready (currently
    /// just <see cref="AdBlockApp.Pages.BrowserPage"/>) without re-downloading/re-parsing filter
    /// lists on a second call — later callers just await the same in-flight/completed task.</summary>
    public Task InitializeAsync() => _initializeTask ??= InitializeCoreAsync();

    private async Task InitializeCoreAsync()
    {
        Settings = AppSettings.Load(Path.Combine(_appDataDir, "settings.json"));
        _provider = new FilterListProvider(Path.Combine(_appDataDir, "filters"));

        var seeded = _provider.LoadCachedEnginesIfPresent();
        if (seeded is not null)
        {
            (Network, Cosmetic) = (seeded.Network, seeded.Cosmetic);
            SetStatus($"Ad-block active (cached) · {Network.RuleCount:N0} rules · updating filters…");
            _ = RefreshInBackgroundAsync();
        }
        else
        {
            var built = await _provider.BuildEnginesAsync(SetStatus);
            (Network, Cosmetic) = (built.Network, built.Cosmetic);
            SetStatus($"Ad-block active · {Network.RuleCount:N0} rules");
        }
    }

    private async Task RefreshInBackgroundAsync()
    {
        var built = await _provider.BuildEnginesAsync(SetStatus);
        (Network, Cosmetic) = (built.Network, built.Cosmetic);
        SetStatus($"Ad-block active · {Network.RuleCount:N0} rules");
        EnginesUpdated?.Invoke();
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
