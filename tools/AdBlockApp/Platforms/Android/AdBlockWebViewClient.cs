using Android.Graphics;
using Android.Webkit;
using AdBlockApp.Services;
using AdBlockCore;
using AWebView = Android.Webkit.WebView;

namespace AdBlockApp.Platforms.Android;

/// <summary>
/// Android's <c>WebViewClient</c> equivalent of the Windows host's <c>WebResourceRequested</c> +
/// <c>NavigationStarting</c> handlers. Replaces MAUI's own <c>MauiWebViewClient</c> entirely (see
/// <c>BrowserPage.Android.cs</c>) rather than subclassing it, because this page does a single
/// fixed navigation and never relies on MAUI's <c>WebView.Navigating</c>/<c>Navigated</c> events —
/// losing them here costs nothing and avoids fighting MAUI's own cancel-then-VirtualView.Navigating
/// plumbing for a page we don't need it on.
/// </summary>
public sealed class AdBlockWebViewClient : WebViewClient
{
    private readonly AdBlockRuntime _runtime;
    private readonly string _homeHost;
    private readonly Action _onRequestBlocked;
    private readonly Action _onHijackBlocked;

    /// <summary>Non-null ONLY when <c>WebViewFeature.DocumentStartScript</c> is unsupported on
    /// this device (see <c>BrowserPage.Android.cs</c>'s <c>InjectCosmeticScriptIfSupported</c>) —
    /// the top-frame-only, no-earlier-than-parser-scripts fallback described in that method's doc
    /// comment. Null on the (expected-common) supported path, where cosmetic injection happens
    /// via <c>WebViewCompat.AddDocumentStartJavaScript</c> instead and this client never touches
    /// cosmetics at all.</summary>
    private readonly Func<string>? _fallbackCosmeticScript;

    public AdBlockWebViewClient(
        AdBlockRuntime runtime, string homeHost, Action onRequestBlocked, Action onHijackBlocked,
        Func<string>? fallbackCosmeticScript = null)
    {
        _runtime = runtime;
        _homeHost = homeHost;
        _onRequestBlocked = onRequestBlocked;
        _onHijackBlocked = onHijackBlocked;
        _fallbackCosmeticScript = fallbackCosmeticScript;
    }

    /// <summary>Top-frame-only cosmetic fallback for devices without DOCUMENT_START_SCRIPT
    /// support — see <see cref="_fallbackCosmeticScript"/>'s doc comment for why this is a
    /// documented, accepted gap rather than parity with the primary path.</summary>
    public override void OnPageStarted(AWebView? view, string? url, Bitmap? favicon)
    {
        base.OnPageStarted(view, url, favicon);
        if (_fallbackCosmeticScript is not null)
            view?.EvaluateJavascript(_fallbackCosmeticScript(), null);
    }

    /// <summary>
    /// THE AD BLOCKER on Android. Per AOSP's WebViewClient javadoc (verified, not assumed):
    /// called on a background thread, not the UI thread — <see cref="AdBlockEngine.ShouldBlock"/>
    /// and <see cref="AppSettings.IsAllowlisted"/> are pure/thread-safe reads so this is safe
    /// without locking. Unlike WebView2's WebResourceRequested, this DOES fire for iframe/
    /// subresource requests (per <c>IWebResourceRequest.IsForMainFrame</c> being false for those)
    /// — exactly the cross-origin-iframe reach the whole app depends on. Returning a non-null
    /// WebResourceResponse cancels the real request; returning null lets it load normally.
    /// </summary>
    public override WebResourceResponse? ShouldInterceptRequest(AWebView? view, IWebResourceRequest? request)
    {
        if (request?.Url is null || _runtime.Network is null || !_runtime.Settings.AdBlockEnabled)
            return base.ShouldInterceptRequest(view, request);

        var url = request.Url.ToString();
        if (url is null) return base.ShouldInterceptRequest(view, request);

        var reqHost = SafeHost(url);
        if (reqHost is not null && _runtime.Settings.IsAllowlisted(reqHost))
            return base.ShouldInterceptRequest(view, request);

        // Android's WebResourceRequest has no separate "document URL" / referrer surface the way
        // WebView2's Headers.GetHeader("Referer") does (verified: IWebResourceRequest exposes
        // RequestHeaders, which MAY include a Referer the app itself set, but not reliably the
        // triggering document for a browser-initiated resource fetch) — pass null and accept that
        // $domain=/third-party rule evaluation degrades to "no document context" on Android,
        // same as AdBlockEngine.ShouldBlock's documented behavior for documentUrl: null.
        string? documentUrl = request.RequestHeaders?.TryGetValue("Referer", out var referer) == true ? referer : null;

        var resourceType = request.IsForMainFrame ? ResourceType.Document : ResourceType.None;
        if (!_runtime.Network.ShouldBlock(url, documentUrl, resourceType))
            return base.ShouldInterceptRequest(view, request);

        _onRequestBlocked();
        return new WebResourceResponse(null, null, null); // empty response == blocked, per AOSP docs
    }

    /// <summary>
    /// Android's equivalent of WebView2's main-frame-only <c>NavigationStarting</c>. Unlike
    /// WebView2, AOSP's javadoc does NOT guarantee this fires only for the main frame — it
    /// explicitly says it "can be called for requests... including... those made from iframes"
    /// (verified against current docs) — so we check <c>IsForMainFrame</c> ourselves rather than
    /// trusting the callback's scope the way the Windows host could trust NavigationStarting's.
    /// Returning true cancels/overrides the navigation.
    /// </summary>
    public override bool ShouldOverrideUrlLoading(AWebView? view, IWebResourceRequest? request)
    {
        if (request?.Url is null || !request.IsForMainFrame)
            return false; // not a top-frame navigation — never our concern, let it proceed

        var url = request.Url.ToString();
        if (url is null || !IsOffSiteTopNavigation(url)) return false;

        _onHijackBlocked();
        return true; // cancel — do not let the top frame leave our own site
    }

    private bool IsOffSiteTopNavigation(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u)) return false;
        if (u.Scheme is not ("http" or "https")) return false;

        var host = u.Host.ToLowerInvariant();
        if (host == _homeHost) return false;
        if (host.EndsWith("." + _homeHost, StringComparison.Ordinal)) return false;
        return true;
    }

    private static string? SafeHost(string uri) =>
        Uri.TryCreate(uri, UriKind.Absolute, out var u) ? u.Host : null;
}
