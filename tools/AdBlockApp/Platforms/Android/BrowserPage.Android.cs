using AdBlockApp.Platforms.Android;
using AdBlockCore;
using AndroidX.WebKit;
using Microsoft.Maui.Handlers;
using AWebView = Android.Webkit.WebView;

namespace AdBlockApp.Pages;

/// <summary>
/// Android half of <see cref="BrowserPage"/>'s native WebView wiring — see the Windows partner
/// (<c>Platforms/Windows/BrowserPage.Windows.cs</c>) for the shared design rationale. Every hook
/// here was chosen against current AOSP/androidx.webkit source (quoted in HANDOFF.md/README.md),
/// not assumed from WebView2 parity — Android's callbacks have materially different guarantees
/// (background-thread delivery, no reliable main-frame-only navigation signal) that the comments
/// below call out explicitly.
/// </summary>
public partial class BrowserPage
{
    private IScriptHandler? _cosmeticScriptHandle;
    private string _homeHost = "localhost";
    private AWebView? _platformWebView;

    private partial Task ConfigureNativeWebViewAsync()
    {
        if (Uri.TryCreate(DefaultStartUrl, UriKind.Absolute, out var home))
            _homeHost = home.Host.ToLowerInvariant();

        if (Web.Handler is not WebViewHandler handler || handler.PlatformView is not AWebView platformView)
            throw new InvalidOperationException("BrowserPage.Web has no Android WebView platform view yet.");

        _platformWebView = platformView;

        // Replace MAUI's default WebViewClient/WebChromeClient outright — see
        // AdBlockWebViewClient's doc comment for why losing MAUI's Navigating/Navigated events
        // is an acceptable trade for this single fixed-navigation page.
        platformView.SetWebViewClient(new AdBlockWebViewClient(Runtime, _homeHost, OnRequestBlocked, OnPopupOrHijackBlocked, OnPageFinishedLoading));

        // The chrome client needs the current Activity to host the HTML5-fullscreen custom view
        // (OnShowCustomView adds it to the Activity's decor). Platform.CurrentActivity is MAUI's
        // supported accessor for it; it's non-null here because ConfigureNativeWebViewAsync runs
        // from the page's Loaded handler, well after the Activity exists.
        var activity = Platform.CurrentActivity
            ?? throw new InvalidOperationException("No current Activity for WebView fullscreen hosting.");
        platformView.SetWebChromeClient(new AdBlockWebChromeClient(activity, OnPopupOrHijackBlocked));

        InjectCosmeticScriptIfSupported(platformView);
        Runtime.EnginesUpdated += () =>
            MainThread.BeginInvokeOnMainThread(() => InjectCosmeticScriptIfSupported(platformView));

        // Unlike Windows, the Android WebView platform view is ready to navigate synchronously
        // once the handler has attached — no separate async "core engine" initialization step.
        return Task.CompletedTask;
    }

    /// <summary>
    /// Cross-frame cosmetic injection on Android — the equivalent of WebView2's
    /// <c>AddScriptToExecuteOnDocumentCreatedAsync</c>. <c>WebViewCompat.AddDocumentStartJavaScript</c>
    /// (androidx.webkit) gives the identical guarantee per its javadoc (quoted, verified against
    /// current AOSP source): runs "before any of the page's JavaScript code" in "any frame whose
    /// origin matches allowedOriginRules" — including cross-origin child iframes, not just the
    /// main frame. It's gated behind <c>WebViewFeature.DocumentStartScript</c>, which depends on
    /// the on-device WebView/Chromium package, NOT just the API level — so we check
    /// <c>IsFeatureSupported</c> at runtime rather than assuming every device running our
    /// SupportedOSPlatformVersion floor has it.
    ///
    /// KNOWN GAP if unsupported (flagged rather than silently shipped, per HANDOFF.md's
    /// instruction to state platform limitations explicitly): the fallback is
    /// <c>WebViewClient.OnPageStarted</c> injection via <c>EvaluateJavascriptAsync</c>, which (a)
    /// only reaches the top frame's own document object at that point — no documented way to
    /// reach a cross-origin child iframe's context from the host at all without
    /// addDocumentStartJavaScript — and (b) is not guaranteed to run before the page's own
    /// inline/parser-blocking scripts. On a device without DOCUMENT_START_SCRIPT support,
    /// cosmetic hiding inside the cross-origin provider iframe DOES NOT WORK — only network
    /// blocking (ShouldInterceptRequest, unaffected by this gap) still functions there. This
    /// mirrors a real WebView/Chromium-version constraint, not an app bug.
    /// </summary>
    private void InjectCosmeticScriptIfSupported(AWebView platformView)
    {
        if (Runtime.Cosmetic is null) return;

        _cosmeticScriptHandle?.Remove();
        _cosmeticScriptHandle = null;

        if (!Runtime.Settings.AdBlockEnabled) return;

        if (!WebViewFeature.IsFeatureSupported(WebViewFeature.DocumentStartScript))
        {
            // Best-effort fallback: top-frame-only, no "before parser-blocking scripts"
            // guarantee. See this method's doc comment — this is a real, documented gap, not
            // a bug to silently "fix" by pretending it works.
            platformView.SetWebViewClient(new AdBlockWebViewClient(
                Runtime, _homeHost, OnRequestBlocked, OnPopupOrHijackBlocked, OnPageFinishedLoading,
                fallbackCosmeticScript: () => CosmeticInjector.BuildScript(Runtime.Cosmetic!, Runtime.Settings.AllowlistedHosts)));
            return;
        }

        var script = CosmeticInjector.BuildScript(Runtime.Cosmetic, Runtime.Settings.AllowlistedHosts);
        // "*" matches every origin — the cosmetic script itself resolves the correct selectors
        // per-frame from location.hostname (see CosmeticInjector's own doc comment), so it's
        // safe and necessary to allow it into every frame, same as WebView2's per-frame reach.
        _cosmeticScriptHandle = WebViewCompat.AddDocumentStartJavaScript(platformView, script, new HashSet<string> { "*" });
    }
}
