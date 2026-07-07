# AdBlockApp — MAUI ad-blocking shell (Windows + Android)

The production MAUI ad-blocking shell. One C# codebase targets Windows (via WebView2) and
Android (via the platform's native WebView), sharing a portable ad-blocking core unmodified.
It began as a Windows-only WPF+WebView2 proof of concept (`tools/AdBlockShell`, since removed
once this app reached feature parity on Windows); [HANDOFF.md](HANDOFF.md) is the original task
spec / design record from that port.

## Architecture

```
tools/
  AdBlockCore/     Portable engine — AbpRule, AdBlockEngine, CosmeticRule, CosmeticEngine,
                    CosmeticInjector, FilterListProvider, PublicSuffixClassifier, AppSettings,
                    UpdateChecker. Zero WPF/WebView2/MAUI/Android references (see its own .csproj
                    comment). Referenced unmodified by AdBlockApp.
  AdBlockApp/       This project. net10.0-windows10.0.19041.0 + net10.0-android, MAUI Shell app.
```

Within `AdBlockApp`:

| Piece | File | Role |
|---|---|---|
| Shared runtime | [Services/AdBlockRuntime.cs](Services/AdBlockRuntime.cs) | Owns `AppSettings` + the network/cosmetic engines, seeds from cache then refreshes live, exposes blocked-request/popup counters. Registered as a DI singleton (see [MauiProgram.cs](MauiProgram.cs)) so `BrowserPage` (filters) and `SettingsPage` (reads/writes settings) share one instance. |
| Browser page | [Pages/BrowserPage.xaml.cs](Pages/BrowserPage.xaml.cs) | Hosts the `WebView`, calls `AdBlockRuntime.InitializeAsync()`, then awaits a per-platform `ConfigureNativeWebViewAsync()` partial method before navigating. |
| Windows native hook-up | [Platforms/Windows/BrowserPage.Windows.cs](Platforms/Windows/BrowserPage.Windows.cs) | Reaches `Microsoft.UI.Xaml.Controls.WebView2`'s `.CoreWebView2` via `Web.Handler.PlatformView` (MAUI's `WebViewHandler` on Windows) and wires `WebResourceRequested`, `NewWindowRequested`, `NavigationStarting`, `AddScriptToExecuteOnDocumentCreatedAsync` — the SAME APIs the WPF PoC used, confirmed by reading dotnet/maui's own handler source. |
| Android native hook-up | [Platforms/Android/BrowserPage.Android.cs](Platforms/Android/BrowserPage.Android.cs), [AdBlockWebViewClient.cs](Platforms/Android/AdBlockWebViewClient.cs), [AdBlockWebChromeClient.cs](Platforms/Android/AdBlockWebChromeClient.cs) | Replaces MAUI's default `WebViewClient`/`WebChromeClient` with custom subclasses implementing `ShouldInterceptRequest` (network blocking), `ShouldOverrideUrlLoading` (hijack-redirect cancellation), `OnCreateWindow` (popup suppression), and `WebViewCompat.AddDocumentStartJavaScript` (cosmetic injection) — see "Platform parity" below for exactly how these map (or don't) onto the Windows behavior. |
| First-run page | [Pages/FirstRunPage.xaml.cs](Pages/FirstRunPage.xaml.cs) | One-time "why does this app exist" explainer, gated on `AppSettings.FirstRunCompleted`. |
| Settings page | [Pages/SettingsPage.xaml.cs](Pages/SettingsPage.xaml.cs) + platform partials | Master toggle, allowlist add/remove/view, filter-list status + manual refresh, app version, manual/auto update check. Update-install is per-platform: [Platforms/Windows/SettingsPage.Windows.cs](Platforms/Windows/SettingsPage.Windows.cs) downloads+relaunches the new exe; [Platforms/Android/SettingsPage.Android.cs](Platforms/Android/SettingsPage.Android.cs) downloads the APK and hands the user to the system installer. |

## Platform parity — what's identical, what genuinely differs

Every claim below was checked against current AOSP/androidx.webkit source or dotnet/maui source
during this port (not assumed from WebView2 familiarity) — see each file's doc comments for the
exact citations/quotes.

- **Network blocking**: identical semantics both platforms. Windows: `WebResourceRequested` (all
  resources, all frames). Android: `WebViewClient.ShouldInterceptRequest` — confirmed it DOES
  fire for iframe/subresource requests (not just top-frame), matching WebView2's reach into the
  cross-origin provider iframe. One real difference: Android's callback runs on a background
  thread (WebView2's runs wherever it runs, undocumented but never an issue in practice) — the
  engine's `ShouldBlock`/`IsAllowlisted` calls are pure reads, so this is safe without locking.
- **Popup/pop-under suppression**: identical outcome. Windows: `NewWindowRequested` +
  `ev.Handled = true`. Android: `WebChromeClient.OnCreateWindow` returning `false` — confirmed
  this is the explicit "deny" signal (and also confirmed MAUI's own `WebChromeClient` doesn't
  override this method at all, so the un-overridden default is already "do nothing"; overriding
  makes that a documented choice instead of an implicit default).
- **Off-site top-frame hijack cancellation**: same intent, WEAKER guarantee on Android. Windows'
  `NavigationStarting` is confirmed main-frame-only per WebView2 docs. Android's
  `ShouldOverrideUrlLoading` is NOT guaranteed main-frame-only per AOSP's own javadoc ("can be
  called for requests... including those from iframes") — `AdBlockWebViewClient` checks
  `IWebResourceRequest.IsForMainFrame` itself to compensate, rather than trusting the callback's
  scope the way the Windows host could.
- **Cosmetic injection into cross-origin iframes — the one real platform gap**: Windows'
  `AddScriptToExecuteOnDocumentCreatedAsync` is unconditionally available and gives the
  before-page-script, all-frames guarantee. Android's equivalent,
  `WebViewCompat.AddDocumentStartJavaScript`, gives an IDENTICAL guarantee per its javadoc
  (verified against current AOSP source) — but ONLY on devices where
  `WebViewFeature.IsFeatureSupported(WebViewFeature.DocumentStartScript)` returns true, which
  depends on the installed WebView/Chromium package, not just the Android API level. On
  unsupported devices, `BrowserPage.Android.cs`'s `InjectCosmeticScriptIfSupported` falls back to
  `WebViewClient.OnPageStarted` + `EvaluateJavascript` — main-frame-only, no "before the page's
  own scripts" guarantee. **On such a device, cosmetic ad-hiding inside the cross-origin
  provider iframe does not work — only network blocking (unaffected by this gap) still does.**
  This is a genuine, documented platform limitation, not a bug papered over.

## Verified vs. not yet verified

**Verified live** (this port), against a real running MoviesMafia dev server
(`https://localhost:5248`) and a real streaming-provider watch page, on Windows:
- The app launches, downloads/loads the full uBO+EasyList+EasyPrivacy+annoyances rule set
  (117,223 network rules in the run that produced this doc), and shows "Ad-block active" with a
  live rule count in the status bar.
- The MoviesMafia homepage and a `/watch/{type}/{id}` page render fully and correctly inside the
  MAUI `WebView` (confirmed via screenshot, not just "no exception thrown").
- The cross-origin streaming-provider iframe (a real embed, "VidAPI" in the verification run)
  loads and its player becomes interactive.
- Clicking into the player triggered a real ad pop-up/hijack attempt from the provider, which was
  caught and suppressed — the status bar's "pop-ups/hijacks blocked" counter incremented, no new
  window opened, and the app never navigated away from the MoviesMafia page.
- Playback then proceeded normally inside the (still cross-origin) provider iframe, confirming
  the blocking hooks do not break the provider's own player.
- A real ordering bug was caught and fixed during this verification: the original
  `ConfigureNativeWebView()` (synchronous, fire-and-forget on Windows) returned before
  `CoreWebView2` finished its async initialization, so `Web.Source = ...` silently raced ahead of
  it and the navigation was dropped — the WebView rendered nothing and no error surfaced anywhere
  (not in the app UI, not in Windows Event Log). Fixed by making the platform hook-up
  (`ConfigureNativeWebViewAsync`) genuinely awaitable on both platforms and awaiting it before
  setting `Source`. This is exactly the class of bug HANDOFF.md's "prove it, don't assume it"
  instruction exists to catch — a MAUI target that builds and even runs is not evidence the
  WebView is actually navigating.

**NOT yet verified** (environment constraint, not skipped by choice): this development machine
has no Android SDK/emulator/device set up for interactive use. The Android target **builds
clean** (confirmed via `dotnet build -f net10.0-android` after installing SDK command-line tools
+ platform 36 + build-tools + a Microsoft OpenJDK 17, entirely for compilation — no emulator was
installed), and every Android-specific API claim above was checked against current AOSP/
androidx.webkit source, but none of it has been exercised on a real device or emulator:
- Network blocking, popup suppression, and hijack cancellation on Android are unverified at
  runtime.
- The `WebViewFeature.DocumentStartScript` support check and its fallback path are unverified at
  runtime — in particular, whether `EvaluateJavascript` fallback timing is early enough to be
  useful at all in practice (see "Platform parity" above) has never been observed, only reasoned
  about from docs.
- The GitHub-Releases APK update flow (`Platforms/Android/SettingsPage.Android.cs`) — the
  `REQUEST_INSTALL_PACKAGES`/`CanRequestPackageInstalls()` gate, the `FileProvider` URI, and the
  install intent — is unverified at runtime.

**Before shipping**, run this app on a real Android device or emulator against a real
MoviesMafia deployment and a real provider watch page, and repeat the same manual proof described
above (ad hosts blocked, popup suppressed, playback intact, cosmetic hiding visible or the
documented fallback gap confirmed). Don't trust "it compiled" for any of the above.

## Running it

```bash
# Windows (uses win-x64 WebView2 target)
dotnet build tools/AdBlockApp/AdBlockApp.csproj -f net10.0-windows10.0.19041.0
tools/AdBlockApp/bin/Debug/net10.0-windows10.0.19041.0/win-x64/AdBlockApp.exe

# Android (requires Android SDK platform 36 + build-tools 36.0.0 + a JDK — see below if missing)
dotnet build tools/AdBlockApp/AdBlockApp.csproj -f net10.0-android
```

If `dotnet build -f net10.0-android` fails with `XA5300: The Android SDK directory could not be
found`, the Android workload's SDK/JDK aren't installed — this is a meaningful local environment
change (disk space, ~1-2GB), not something to do silently. Minimal command-line-only setup (no
Visual Studio, no emulator) that was used to verify this project compiles:

```bash
# 1. Android SDK command-line tools + platform 36 + build-tools
#    (download commandlinetools-win-*.zip from https://developer.android.com/studio#command-tools,
#    unzip so sdkmanager.bat ends up at <sdk>/cmdline-tools/latest/bin/)
<sdk>/cmdline-tools/latest/bin/sdkmanager.bat --sdk_root=<sdk> "platform-tools" "platforms;android-36" "build-tools;36.0.0"

# 2. A JDK (Microsoft's build works well): https://aka.ms/download-jdk/microsoft-jdk-17-windows-x64.zip

# 3. Build, pointing at both:
dotnet build tools/AdBlockApp/AdBlockApp.csproj -f net10.0-android -p:AndroidSdkDirectory=<sdk> -p:JavaSdkDirectory=<jdk-dir>
```

The default `Web.Source` is `https://moviesmafia.runasp.net` (the production site). For local
development against the dev server, temporarily point `BrowserPage.DefaultStartUrl` at
`https://localhost:5248` — the Windows platform code still trusts that self-signed cert.

## Releasing (CI — the normal path)

Releases are built by [.github/workflows/release-apps.yml](../../.github/workflows/release-apps.yml),
which runs **only on a version tag** (not on every push to master — the website has its own
`deploy.yml`). To cut a release:

```bash
git tag v1.2.0
git push origin v1.2.0
```

The workflow: resolves the version from the tag (stamps `ApplicationDisplayVersion`), builds the
Windows folder + zips it (`MoviesMafia-win-x64-v1.2.0.zip`), builds the signed Android APK
(`MoviesMafia-v1.2.0.apk`), and publishes both to a GitHub Release named for the tag.
`UpdateChecker` (in `AdBlockCore`) then finds it via `GET /repos/im-ashar/MoviesMafia/releases/latest`
and picks the `win`+`.zip` asset (Windows) or `.apk` (Android).

**Required repo secrets** (Settings → Secrets and variables → Actions) for the Android signing
job — the keystore stays out of git and is injected at build time:
- `ANDROID_KEYSTORE_BASE64` — your keystore, base64-encoded (`base64 -w0 my.keystore`).
- `ANDROID_KEYSTORE_PASSWORD`, `ANDROID_KEY_ALIAS`, `ANDROID_KEY_PASSWORD`.

> **The keystore must never change between releases.** Android refuses to install an update whose
> APK is signed with a different key than the installed one, and the failure is silent from the
> app's side. Generate it once, store it as the secret, keep it forever. (No keystore secret set?
> The build falls back to the committed `signing/adblockapp-debug.keystore` — fine for local dev
> builds, but a first CI release should establish the real, permanent one.)

### Manual local publish (for testing, not releases)

```bash
# Windows — self-contained WindowsAppSDK folder (NOT a single exe; PublishSingleFile is
# unsupported here and ignored). Distribute the folder as a zip.
dotnet publish tools/AdBlockApp/AdBlockApp.csproj -f net10.0-windows10.0.19041.0 -c Release
# → tools/AdBlockApp/bin/Release/net10.0-windows10.0.19041.0/win-x64/publish/  (~500 files)

# Android — signed release APK (uses the committed debug keystore unless the ADBLOCKAPP_ANDROID_*
# vars below are set).
dotnet publish tools/AdBlockApp/AdBlockApp.csproj -f net10.0-android -c Release
# → tools/AdBlockApp/bin/Release/net10.0-android/*-Signed.apk
```

## Signing hooks (for plugging in a real cert later)

Per HANDOFF.md's decision: ship unsigned (Windows) / self-signed (Android) today, with one
documented env-var hook per platform. CI uses these for Android (above); locally they're optional.

**Android** — [AdBlockApp.csproj](AdBlockApp.csproj)'s signing `PropertyGroup`:
- Default (no vars): committed `signing/adblockapp-debug.keystore` (password/alias `adblockapp`).
  Public by design — it exists purely so update-signatures stay consistent, not for security.
- Override: set `ADBLOCKAPP_ANDROID_KEYSTORE_PATH`, `ADBLOCKAPP_ANDROID_STOREPASS`,
  `ADBLOCKAPP_ANDROID_KEYALIAS`, `ADBLOCKAPP_ANDROID_KEYPASS` (env or `-p:`). The CI workflow sets
  exactly these from the repo secrets.

**Windows** — [AdBlockApp.csproj](AdBlockApp.csproj)'s `SignWindowsPublishOutput` target
(`AfterTargets="Publish"`):
- Default: no-op (runs only if `ADBLOCKAPP_WIN_CERT_PATH` is set) — the build ships unsigned;
  users see an "unknown publisher" SmartScreen prompt on first run.
- Override: set `ADBLOCKAPP_WIN_CERT_PATH` (a `.pfx`) and `ADBLOCKAPP_WIN_CERT_PASSWORD` before
  `dotnet publish` — the target Authenticode-signs `AdBlockApp.exe` inside the published folder.

## Auto-update

[AdBlockCore/UpdateChecker.cs](../AdBlockCore/UpdateChecker.cs) is the shared, portable piece:
calls GitHub's public Releases API (no auth needed), compares the returned tag against
`AppInfo.Current.VersionString`, and returns asset URLs. Never throws — every failure path
(offline, rate-limited, malformed response, no releases yet) returns `null`, so a broken update
check can never crash or block the app.

- **Windows**: [Platforms/Windows/SettingsPage.Windows.cs](Platforms/Windows/SettingsPage.Windows.cs)
  does NOT self-install — the app is a ~500-file WindowsAppSDK folder, not a single swappable exe.
  It opens the release's Windows `.zip` download (or the release page) in the browser; the user
  extracts it over their existing install. Simple and robust; no fragile self-replace-while-running.
- **Android**: [Platforms/Android/SettingsPage.Android.cs](Platforms/Android/SettingsPage.Android.cs)
  downloads the release's `.apk` asset into the app's external-files "updates" folder, then
  launches `Intent.ActionView` on a `content://` URI (via `FileProvider` — see
  `AndroidManifest.xml`'s `<provider>` entry and `Resources/xml/file_paths.xml`) with MIME type
  `application/vnd.android.package-archive`. This requires the "Install unknown apps" special
  permission (Android 8.0+, `REQUEST_INSTALL_PACKAGES` — a per-app Settings toggle, not a runtime
  dialog) to be granted for this app's package; if `PackageManager.CanRequestPackageInstalls()`
  returns false, the user is sent straight to `Settings.ActionManageUnknownAppSources` instead of
  the install intent. **This requirement is a real, user-visible extra step on first update** —
  document it in your own release notes/support materials, since a user who denies it will see
  the update silently fail to prompt an install with no error message.
- Both platforms check are surfaced only through the Settings page's "Check for app update now"
  button today (no automatic background check on launch) — `AppSettings.AutoUpdateEnabled` exists
  for a future background-check policy but nothing currently reads it to trigger one
  automatically; wiring that up is a small addition to `AdBlockRuntime`/`App.xaml.cs` if wanted.
