using AdBlockCore;
using Microsoft.Maui.Handlers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace AdBlockApp.Pages;

/// <summary>
/// Windows half of <see cref="BrowserPage"/>'s native WebView wiring. <c>Microsoft.Maui.Controls.WebView</c>
/// doesn't expose <c>WebResourceRequested</c>/<c>NewWindowRequested</c>/<c>NavigationStarting</c>/
/// <c>AddScriptToExecuteOnDocumentCreatedAsync</c> itself, so we drop to the platform view: MAUI's
/// Windows <c>WebViewHandler</c> creates a <c>MauiWebView</c> which derives from
/// <c>Microsoft.UI.Xaml.Controls.WebView2</c> (the WinUI wrapper, NOT WPF's) — confirmed by reading
/// dotnet/maui's <c>WebViewHandler.Windows.cs</c>/<c>MauiWebView.cs</c> source directly. That WinUI
/// control's <c>.CoreWebView2</c> is the same <see cref="CoreWebView2"/> the WPF PoC used, so every
/// hook below is byte-for-byte the same API the WPF host already proved works — see
/// AdBlockShell/MainWindow.xaml.cs and README.md's "Verifying it works".
/// </summary>
public partial class BrowserPage
{
    private string? _cosmeticScriptId;
    private string _homeHost = "localhost";
    private CoreWebView2? _core;

    private partial async Task ConfigureNativeWebViewAsync()
    {
        if (Uri.TryCreate(DefaultStartUrl, UriKind.Absolute, out var home))
            _homeHost = home.Host.ToLowerInvariant();

        if (Web.Handler is not WebViewHandler handler || handler.PlatformView is not WebView2 platformView)
            throw new InvalidOperationException("BrowserPage.Web has no Windows WebView2 platform view yet.");

        // MauiWebView (dotnet/maui's platform view) itself calls EnsureCoreWebView2Async lazily
        // on first navigation, but CoreWebView2 stays null until that completes — awaiting it
        // here ourselves, before returning, is what guarantees OnPageLoaded's subsequent
        // `Web.Source = ...` actually reaches a live CoreWebView2 instead of racing its async
        // creation and being silently dropped (confirmed empirically: without this await, the
        // WebView2 control initializes AFTER Source is set and the navigation never happens).
        await platformView.EnsureCoreWebView2Async();
        WireCore(platformView.CoreWebView2);
    }

    private void WireCore(CoreWebView2 core)
    {
        _core = core;

        // 1) THE AD BLOCKER — identical filter registration to the WPF PoC.
        core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += OnWebResourceRequested;

        // 2) Cosmetic hiding — inject once the engines are ready; InitializeAsync has already
        // completed by the time ConfigureNativeWebView runs (see OnPageLoaded), so Runtime.Cosmetic
        // is non-null here on the happy path.
        _ = WireCosmeticInjectionAsync(core);
        Runtime.EnginesUpdated += () => MainThread.BeginInvokeOnMainThread(() => _ = WireCosmeticInjectionAsync(core));

        // 3) Pop-ups/pop-unders — suppress outright, exactly like the WPF PoC: mark handled,
        // never navigate the main view to the popup's URL.
        core.NewWindowRequested += (_, ev) =>
        {
            ev.Handled = true;
            OnPopupOrHijackBlocked();
        };

        // 4) Off-site top-frame hijack redirects — NavigationStarting is confirmed main-frame-only
        // per WebView2 docs (verified when building the WPF PoC), so the provider's cross-origin
        // iframe navigating via the separate FrameNavigationStarting is untouched.
        core.NavigationStarting += (_, ev) =>
        {
            if (IsOffSiteTopNavigation(ev.Uri))
            {
                ev.Cancel = true;
                OnPopupOrHijackBlocked();
            }
        };

        // 5) Trust the MoviesMafia dev server's self-signed cert for localhost, same as the WPF PoC.
        core.ServerCertificateErrorDetected += (_, ev) =>
        {
            var host = SafeHost(ev.RequestUri);
            if (host is "localhost" or "127.0.0.1")
                ev.Action = CoreWebView2ServerCertificateErrorAction.AlwaysAllow;
        };
    }

    private async Task WireCosmeticInjectionAsync(CoreWebView2 core)
    {
        if (Runtime.Cosmetic is null) return;

        if (_cosmeticScriptId is not null)
            core.RemoveScriptToExecuteOnDocumentCreated(_cosmeticScriptId);

        if (!Runtime.Settings.AdBlockEnabled)
        {
            _cosmeticScriptId = null;
            return;
        }

        var script = CosmeticInjector.BuildScript(Runtime.Cosmetic, Runtime.Settings.AllowlistedHosts);
        _cosmeticScriptId = await core.AddScriptToExecuteOnDocumentCreatedAsync(script);
    }

    private void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        if (Runtime.Network is null || !Runtime.Settings.AdBlockEnabled) return;

        var url = e.Request.Uri;
        string? documentUrl =
            e.Request.Headers.Contains("Referer") ? e.Request.Headers.GetHeader("Referer") : null;

        if (SafeHost(url) is { } reqHost && Runtime.Settings.IsAllowlisted(reqHost)) return;
        if (documentUrl is not null && SafeHost(documentUrl) is { } docHost && Runtime.Settings.IsAllowlisted(docHost)) return;

        var resourceType = ToResourceType(e.ResourceContext);
        if (!Runtime.Network.ShouldBlock(url, documentUrl, resourceType)) return;

        e.Response = _core!.Environment.CreateWebResourceResponse(
            Content: null, StatusCode: 403, ReasonPhrase: "Blocked by AdBlockApp", Headers: "");

        OnRequestBlocked();
    }

    private static string? SafeHost(string uri) =>
        Uri.TryCreate(uri, UriKind.Absolute, out var u) ? u.Host : null;

    private bool IsOffSiteTopNavigation(string uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var u)) return false;
        if (u.Scheme is not ("http" or "https")) return false;

        var host = u.Host.ToLowerInvariant();
        if (host == _homeHost) return false;
        if (host.EndsWith("." + _homeHost, StringComparison.Ordinal)) return false;
        return true;
    }

    private static ResourceType ToResourceType(CoreWebView2WebResourceContext context) => context switch
    {
        CoreWebView2WebResourceContext.Document => ResourceType.Document,
        CoreWebView2WebResourceContext.Stylesheet => ResourceType.Stylesheet,
        CoreWebView2WebResourceContext.Image => ResourceType.Image,
        CoreWebView2WebResourceContext.Media => ResourceType.Media,
        CoreWebView2WebResourceContext.Font => ResourceType.Font,
        CoreWebView2WebResourceContext.Script => ResourceType.Script,
        CoreWebView2WebResourceContext.XmlHttpRequest => ResourceType.XmlHttpRequest,
        CoreWebView2WebResourceContext.Fetch => ResourceType.XmlHttpRequest,
        CoreWebView2WebResourceContext.EventSource => ResourceType.XmlHttpRequest,
        CoreWebView2WebResourceContext.Websocket => ResourceType.WebSocket,
        CoreWebView2WebResourceContext.Ping => ResourceType.Ping,
        CoreWebView2WebResourceContext.TextTrack => ResourceType.Other,
        CoreWebView2WebResourceContext.Manifest => ResourceType.Other,
        CoreWebView2WebResourceContext.SignedExchange => ResourceType.Other,
        CoreWebView2WebResourceContext.CspViolationReport => ResourceType.Other,
        CoreWebView2WebResourceContext.Other => ResourceType.Other,
        _ => ResourceType.None,
    };
}
