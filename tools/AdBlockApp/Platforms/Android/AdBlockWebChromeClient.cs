using Android.Webkit;
using AWebView = Android.Webkit.WebView;
using OsMessage = Android.OS.Message;

namespace AdBlockApp.Platforms.Android;

/// <summary>
/// Popup/pop-under suppression on Android — the equivalent of the Windows host's
/// <c>NewWindowRequested</c> handler. Per AOSP's <c>WebChromeClient</c> javadoc and MAUI's own
/// source (<c>MauiWebChromeClient</c> does NOT override <c>OnCreateWindow</c>, confirmed by
/// reading dotnet/maui), the un-overridden default is that <c>target="_blank"</c>/<c>window.open</c>
/// simply does nothing — no popup, no navigation. Overriding and explicitly returning
/// <c>false</c> makes that "deny" behavior our own documented choice rather than an accident of
/// MAUI's default, so a future MAUI upgrade that changes the default can't silently reopen
/// this hole.
/// </summary>
public sealed class AdBlockWebChromeClient : WebChromeClient
{
    private readonly Action _onPopupBlocked;

    public AdBlockWebChromeClient(Action onPopupBlocked) => _onPopupBlocked = onPopupBlocked;

    public override bool OnCreateWindow(AWebView? view, bool isDialog, bool isUserGesture, OsMessage? resultMsg)
    {
        _onPopupBlocked();
        return false; // deny — do not create/attach a transport WebView for the new window
    }
}
