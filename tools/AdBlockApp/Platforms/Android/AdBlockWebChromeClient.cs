using Android.App;
using Android.Content.PM;
using Android.Views;
using Android.Webkit;
using Android.Widget;
using AndroidX.Core.View;
using AWebView = Android.Webkit.WebView;
using AView = Android.Views.View;   // disambiguate from Microsoft.Maui.Controls.View (implicit usings)
using OsMessage = Android.OS.Message;

namespace AdBlockApp.Platforms.Android;

/// <summary>
/// Popup/pop-under suppression + HTML5 video fullscreen on Android — the equivalent of the
/// Windows host's <c>NewWindowRequested</c> handler and WebView2's fullscreen support.
///
/// Popup: per AOSP's <c>WebChromeClient</c> javadoc and MAUI's own source (<c>MauiWebChromeClient</c>
/// does NOT override <c>OnCreateWindow</c>, confirmed by reading dotnet/maui), the un-overridden
/// default is that <c>target="_blank"</c>/<c>window.open</c> simply does nothing — no popup, no
/// navigation. Overriding and explicitly returning <c>false</c> makes that "deny" behavior our own
/// documented choice rather than an accident of MAUI's default, so a future MAUI upgrade that
/// changes the default can't silently reopen this hole.
///
/// Fullscreen: when the page's player enters HTML5 fullscreen (the provider's fullscreen button),
/// the WebView hands us a native view via <c>OnShowCustomView</c> and expects the HOST to display
/// it edge-to-edge — MAUI's default chrome client does not, so the button appeared dead. We attach
/// that view over the Activity's decor view, hide the system bars, and rotate to landscape;
/// <c>OnHideCustomView</c> reverses all of it. This is the standard AOSP-documented custom-view
/// contract, not a hack.
/// </summary>
public sealed class AdBlockWebChromeClient : WebChromeClient
{
    private readonly Activity _activity;
    private readonly Action _onPopupBlocked;

    // Fullscreen state — non-null only while a custom (fullscreen) view is showing. We stash the
    // pre-fullscreen orientation so OnHideCustomView can restore exactly what the user had, rather
    // than guessing a default.
    private AView? _customView;
    private ICustomViewCallback? _customViewCallback;
    private ScreenOrientation _originalOrientation;

    public AdBlockWebChromeClient(Activity activity, Action onPopupBlocked)
    {
        _activity = activity;
        _onPopupBlocked = onPopupBlocked;
    }

    public override bool OnCreateWindow(AWebView? view, bool isDialog, bool isUserGesture, OsMessage? resultMsg)
    {
        _onPopupBlocked();
        return false; // deny — do not create/attach a transport WebView for the new window
    }

    /// <summary>Player entered HTML5 fullscreen. AOSP contract: add <paramref name="view"/> to the
    /// window at full size and keep <paramref name="callback"/> to notify the WebView when we
    /// leave. Guard against a double-show (some players fire twice) by hiding the first.</summary>
    public override void OnShowCustomView(AView? view, ICustomViewCallback? callback)
    {
        if (_customView is not null)
        {
            // Already fullscreen — reject the second view per the AOSP contract (tell the WebView
            // to tear the new one down) instead of leaking overlapping decor children.
            callback?.OnCustomViewHidden();
            return;
        }

        _customView = view;
        _customViewCallback = callback;
        _originalOrientation = _activity.RequestedOrientation;

        // Full-bleed video is expected landscape; rotate and let it follow the sensor so the user
        // can flip either way.
        _activity.RequestedOrientation = ScreenOrientation.SensorLandscape;

        var decor = (ViewGroup)_activity.Window!.DecorView;
        decor.AddView(_customView, new FrameLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));

        SetSystemBarsVisible(false);
    }

    /// <summary>Player left fullscreen (or the app pulled it out). Reverse everything
    /// OnShowCustomView did and tell the WebView we're done via the stored callback.</summary>
    public override void OnHideCustomView()
    {
        if (_customView is null) return;

        var decor = (ViewGroup)_activity.Window!.DecorView;
        decor.RemoveView(_customView);
        _customView = null;

        SetSystemBarsVisible(true);
        _activity.RequestedOrientation = _originalOrientation;

        _customViewCallback?.OnCustomViewHidden();
        _customViewCallback = null;
    }

    /// <summary>Hide/show the status + navigation bars for immersive fullscreen. Uses AndroidX's
    /// WindowInsetsControllerCompat (the non-deprecated, cross-version path — the raw
    /// SystemUiVisibility flags are deprecated on API 30+; androidx.core is already a dependency).</summary>
    private void SetSystemBarsVisible(bool visible)
    {
        var window = _activity.Window!;
        WindowCompat.SetDecorFitsSystemWindows(window, visible);
        var controller = WindowCompat.GetInsetsController(window, window.DecorView);
        if (controller is null) return; // no inset controller (shouldn't happen post-attach) — nothing to toggle
        if (visible)
        {
            controller.Show(WindowInsetsCompat.Type.SystemBars());
        }
        else
        {
            controller.Hide(WindowInsetsCompat.Type.SystemBars());
            // Let the user reveal the bars with a swipe without leaving fullscreen.
            controller.SystemBarsBehavior = WindowInsetsControllerCompat.BehaviorShowTransientBarsBySwipe;
        }
    }
}
