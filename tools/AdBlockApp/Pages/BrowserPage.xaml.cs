using AdBlockApp.Services;

namespace AdBlockApp.Pages;

/// <summary>
/// The main watch-page shell — one <see cref="AdBlockRuntime"/> (network/cosmetic engines,
/// settings) shared across platforms, plus a platform-specific native hook-up implemented as a
/// partial method in <c>Platforms/Windows/BrowserPage.Windows.cs</c> and
/// <c>Platforms/Android/BrowserPage.Android.cs</c> (MAUI's Platforms/&lt;X&gt; folder convention
/// restricts each file to its own TFM automatically, same as <c>MainActivity.cs</c>/
/// <c>App.xaml.cs</c> already do — no <c>#if</c> guards needed here).
///
/// The split exists because <c>Microsoft.Maui.Controls.WebView</c> does NOT expose
/// WebResourceRequested/NewWindowRequested/NavigationStarting/AddScriptToExecuteOnDocumentCreatedAsync
/// (Windows) or shouldInterceptRequest/OnCreateWindow/shouldOverrideUrlLoading/
/// addDocumentStartJavaScript (Android) itself — those only exist on the underlying native
/// control (<c>Microsoft.UI.Xaml.Controls.WebView2</c> / <c>Android.Webkit.WebView</c>), reached
/// via <c>Web.Handler.PlatformView</c> once the handler attaches. See HANDOFF.md Task 1.
/// </summary>
public partial class BrowserPage : ContentPage
{
    // The production MoviesMafia site the shell wraps. For local development against the dev
    // server (https://localhost:5248), temporarily point this there — the Windows platform code
    // still trusts the localhost self-signed cert so that works, but shipped builds must target
    // the real origin so the in-app updater and cert handling behave normally.
    public const string DefaultStartUrl = "https://moviesmafia.runasp.net";

    public readonly AdBlockRuntime Runtime;

    public BrowserPage(AdBlockRuntime runtime)
    {
        Runtime = runtime;
        InitializeComponent();
        Loaded += OnPageLoaded;
    }

    private async void OnPageLoaded(object? sender, EventArgs e)
    {
        Loaded -= OnPageLoaded;

        await Runtime.InitializeAsync();

        try
        {
            // Must be fully awaited before setting Web.Source below: on Windows, the platform
            // WebView2 control's CoreWebView2 isn't ready synchronously (it's created via
            // EnsureCoreWebView2Async) — setting Source before that completes silently drops
            // the navigation because MAUI's handler has nothing to hand it to yet. Every
            // platform partial's implementation is responsible for not returning until its
            // native WebView is actually ready to navigate.
            await ConfigureNativeWebViewAsync();
        }
        catch
        {
            // The native hook-up failed — still navigate so the user sees the site (unfiltered)
            // rather than a blank screen. There's no status bar to report the error to anymore;
            // this is a wrapper, not a browser. (Ad-block wiring failing is rare and non-fatal.)
        }

        Web.Source = DefaultStartUrl;
    }

    private async void OnSettingsClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync(nameof(SettingsPage));

    /// <summary>Called by the platform partial after it cancels a request that matched a network
    /// block rule. Kept as a live tally on the shared runtime (surfaced in Settings' filter
    /// status); the main page shows no counter — it's a chrome-less site wrapper.</summary>
    protected void OnRequestBlocked() => Runtime.RecordBlockedRequest();

    /// <summary>Called by the platform partial after it suppresses a pop-up/pop-under or
    /// cancels an off-site top-frame hijack redirect. Tallied on the runtime, not shown on-page.</summary>
    protected void OnPopupOrHijackBlocked() => Runtime.RecordBlockedPopup();

    /// <summary>Implemented per-platform: wires network interception, popup suppression,
    /// off-site navigation cancellation, and cosmetic script injection onto the native WebView
    /// control underneath <see cref="Web"/>. Must not return until the native WebView is ready
    /// to accept a navigation (see <see cref="OnPageLoaded"/>'s comment on why this matters).</summary>
    private partial Task ConfigureNativeWebViewAsync();
}
