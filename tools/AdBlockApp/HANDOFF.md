# Implementation Handoff — Production-grade Ad-Blocking Shell (Windows + Android)

> **Status: implemented.** The MAUI port (`tools/AdBlockApp/`), settings UI, GitHub-Releases
> auto-update, and packaging/signing hooks described below are done — see
> [tools/AdBlockApp/README.md](../AdBlockApp/README.md) for the architecture, what was verified
> live vs. not (Android has no local SDK/device in this environment — see that README's
> "Verified vs. not yet verified" section for the exact boundary), and exact release commands.
> This file is kept as the original task spec / design record; don't re-derive decisions already
> made here.

## Context (read this first)

MoviesMafia is an ASP.NET Core / Blazor (Reactive SSR) app at repo root
`c:\Users\m.ashar\source\repos\MoviesMafia`, targeting `net10.0`, served in dev on
**https://localhost:5248** (self-signed cert). It streams movies/series by embedding
**third-party providers in a cross-origin `<iframe>`** — see
[Components/Reactive/SourcePlayer.razor](../../Components/Reactive/SourcePlayer.razor). Those
providers inject aggressive ads: pop-ups, pop-unders, click-to-redirect hijacks, and inline ad
containers.

**The hard constraint:** the ads live inside a *cross-origin* iframe, so the browser's same-origin
policy makes it impossible for the website's own JavaScript to touch them. Extension (uBlock), DNS
(Pi-hole), and iframe `sandbox` were all rejected by the owner (per-device install / providers
detect sandbox / no central control). The chosen solution: a **native host app that owns the
WebView and blocks ads from the host layer** — the same privileged position uBlock Origin
occupies, but shipped as an app instead of a browser extension.

## What already exists — a hardened Windows PoC, not a toy

`tools/AdBlockShell/` is a **working, verified Windows WPF + WebView2 app**. It is deliberately
**excluded from `MoviesMafia.sln`** and from the web project's compilation — see the
`<Compile Remove="tools/**/*.cs" />` block in [MoviesMafia.csproj](../../MoviesMafia.csproj). The
Web SDK's implicit file globbing would otherwise pull WPF files into the web app's build and break
it; **keep that exclusion intact** no matter how you restructure `tools/`.

Read [README.md](README.md) in full before touching anything — it documents the current
architecture, verified behavior, and known limits in detail. Summary of what's real and tested:

- **Network blocking**: [AdBlock/AbpRule.cs](AdBlock/AbpRule.cs) + [AdBlock/AdBlockEngine.cs](AdBlock/AdBlockEngine.cs) parse and match real EasyList/uBO network rules (~117k rules), including `$domain=`, `$third-party`, and resource-**type** options (`$script`/`$image`/etc., via [AdBlock/ResourceType.cs](AdBlock/ResourceType.cs)). Any option the engine can't fully honor causes the WHOLE rule to be dropped, never partially applied — this policy caught a real bug (a scoped catch-all rule was blocking the provider's own `player.js` until this fix landed). Third-party classification uses a real Public Suffix List ([AdBlock/PublicSuffixClassifier.cs](AdBlock/PublicSuffixClassifier.cs), via `Nager.PublicSuffix`), not a naive heuristic.
- **Cosmetic filtering**: [AdBlock/CosmeticRule.cs](AdBlock/CosmeticRule.cs) + [AdBlock/CosmeticEngine.cs](AdBlock/CosmeticEngine.cs) parse plain `##`/`#@#` selector rules (generic, domain-scoped, exceptions). A safety guard rejects bare/unqualified selectors (`video`, `iframe`, `*`, `body`) that would risk hiding the player. Procedural (`#?#`) and scriptlet (`##+js`, `#$#`) rules are recognized but explicitly rejected and counted (`SkippedUnsupported`) — NOT silently dropped as if handled. [AdBlock/CosmeticInjector.cs](AdBlock/CosmeticInjector.cs) serializes the whole rule table into one JS payload registered via `CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync`, verified against Microsoft's current docs to run in **every frame including cross-origin child iframes**, before the frame's own scripts execute. Each frame resolves its own selectors from `location.hostname` synchronously (see that file's doc comment for why this design beats a postMessage round-trip or a host-precomputed stylesheet).
- **Popup/hijack suppression**: `NewWindowRequested` is fully suppressed (nothing opens — earlier version tried to redirect the ad URL into the main view, which was a real, since-fixed bug). Off-site top-frame navigations are cancelled via `NavigationStarting`, verified to be main-frame-only per WebView2 docs, so it cannot break the provider's iframe (which navigates via the separate, unhandled `FrameNavigationStarting`).
- **Filter list management**: [AdBlock/FilterListProvider.cs](AdBlock/FilterListProvider.cs) downloads uBO's default lists + EasyList/EasyPrivacy/annoyances, caches under `%LOCALAPPDATA%\AdBlockShell\filters`, seeds instantly from cache on startup (`LoadCachedEnginesIfPresent`), refreshes in the background on a 4-day cadence, falls back to cache offline.
- **Settings**: [AdBlock/AppSettings.cs](AdBlock/AppSettings.cs) — master toggle + per-site allowlist, JSON-persisted, no WebView2/WPF dependency.
- **Packaging**: `dotnet publish -c Release` already produces a self-contained single-file `win-x64` exe (see the `Release`-conditioned `PropertyGroup` in [AdBlockShell.csproj](AdBlockShell.csproj)). WebView2 Runtime is a separate dependency — in-box on Windows 11, needs the Evergreen bootstrapper on Windows 10 (documented in README).

**The `AdBlock/` engine is already plain, portable .NET with zero WPF/WebView2 references** — this
was intentional so a future cross-platform shell could reuse it. That future is now.

**Verification approach already established and expected to continue:** a throwaway console
project referencing `AdBlock/*.cs` files directly, loading real live filter lists, and asserting
specific URLs/hosts/selectors behave as expected (block vs. pass, hidden vs. visible). This is how
every bug so far was actually caught — don't skip it for new work.

---

## What "production" means for this task (decided, not open questions)

The following have been decided with the project owner — treat them as fixed constraints, not
options to revisit:

1. **Architecture: .NET MAUI.** One C# codebase targets Windows (via WebView2) and Android (via
   the platform's native WebView), sharing the `AdBlock/` engine untouched. This also opens the
   door to hosting the site's own Blazor components directly via `BlazorWebView` instead of just
   pointing the WebView at the live URL — evaluate this as an option but it is not required; a
   thin WebView shell pointed at the deployed site is an acceptable MAUI implementation too.
2. **Distribution: direct download, not app stores.** Signed installers/APKs hosted as **GitHub
   Releases** on this repo (`https://github.com/im-ashar/MoviesMafia`). No Play Store / Microsoft
   Store submission — do not build store-specific packaging (no MSIX-for-Store, no Play App
   Bundle signing config) unless explicitly asked later.
3. **Code signing: start unsigned.** No trusted certificate exists yet for either platform. Ship
   Windows and Android builds **unsigned** (or self-signed for Android's mandatory APK signature —
   Android requires *some* signature even for direct-install APKs; a debug/self-signed keystore is
   fine for now). **Design the build/release pipeline so a real certificate slots in later via a
   secret/config value with no code changes** — e.g. an MSBuild property or CI secret name that,
   if absent, falls back to unsigned/self-signed, and if present, signs for real. Document exactly
   where that hook is.
4. **Auto-update: GitHub Releases as the update source.** The app should check GitHub's Releases
   API for a newer version tag than its own and prompt the user to download. Implement this for
   both platforms in a way that shares logic (version-compare, release-note fetch) through the
   portable layer, with only the "how do I actually install the new binary" step being
   platform-specific (Windows: download+run the new installer/exe and exit; Android: cannot
   silently self-update an unsigned APK across a version change with a different signature unless
   the signature stays consistent build-to-build — use a **consistent debug/self-signed keystore
   checked into a secrets-safe location or generated once and reused**, not a fresh throwaway key
   per build, or Android will refuse the update as a signature mismatch).
5. **Polish scope: settings UI + code-signing hook, not full test/CI/telemetry buildout.** The
   owner explicitly scoped this round to **(a)** turning the current bare toggle/allowlist buttons
   into a proper settings screen and **(b)** the signing/update hooks above. Automated test suites,
   CI pipelines, and crash telemetry were explicitly **not** requested this round — don't build
   them speculatively. (If you judge a *small* amount of testing is necessary to safely verify a
   MAUI port didn't regress the engine, that's fine and expected — see Task 1 — but don't turn it
   into a general test-infrastructure project.)

---

## Your tasks

### 1. Port to .NET MAUI — Windows (WebView2) + Android (native WebView)

- Scaffold a MAUI app (likely a new `tools/AdBlockApp/` project, or restructure — your call, but
  justify it in the PR/commit) targeting `net10.0-android` and `net10.0-windows10.0.x` (WinUI/MAUI
  Windows target, NOT the current raw WPF host — MAUI's Windows target uses `WebView2` internally
  through `Microsoft.Maui.Controls.WebView`, but you'll likely need the **platform-specific
  handler** to reach `CoreWebView2` directly for `WebResourceRequested`, `NewWindowRequested`,
  `NavigationStarting`, and `AddScriptToExecuteOnDocumentCreatedAsync` — verify current MAUI
  BlazorWebView/WebView platform-handler APIs against Microsoft's docs before assuming parity with
  raw WebView2; do not assume MAUI's `WebView` control exposes these by default.
- **Reference the existing `AdBlock/*.cs` files unmodified** as shared code (project reference or
  linked files) — do not fork/duplicate the engine. If you need to adjust anything in `AdBlock/`
  to remove a lingering platform assumption, that's acceptable, but keep it platform-agnostic.
- **Android's WebView interception equivalent:** `shouldInterceptRequest` (via
  `WebViewClient`/`WebResourceRequestHandler` in .NET for Android) is the network-blocking hook;
  it does NOT receive the same rich request object as WebView2 (verify exactly what's available —
  headers, resource type, referrer — against current Android WebView / .NET for Android docs
  before assuming `AdBlockEngine.ShouldBlock`'s signature maps over 1:1). Popup suppression on
  Android is `WebChromeClient.OnCreateWindow` (verify: does it default to allow or deny returning
  `false`? get this right, don't guess). Cosmetic injection into cross-origin Android iframes:
  confirm `WebView.EvaluateJavascriptAsync` / `addJavascriptInterface` timing gives you the same
  "before the frame's own scripts run" guarantee that `AddScriptToExecuteOnDocumentCreatedAsync`
  gives on WebView2 — if it doesn't, you may need `WebViewClient.OnPageStarted` injection or a
  different mechanism; **prove this on a real device/emulator against a real provider embed, the
  same way the WebView2 cosmetic injection was proven** (see README's "Verifying it works"), don't
  ship assuming it works.
- **Prove parity, don't assume it.** Minimum verification before calling the port done, on BOTH
  platforms against a real running MoviesMafia instance and a real provider watch page:
  - Real ad hosts (googletagmanager, doubleclick, etc.) are blocked.
  - The provider's own player script and the app's own assets are NOT blocked.
  - A pop-up/pop-under click does not open a new window/tab.
  - A click-hijack redirect does not navigate the app away from the site.
  - A known ad-container selector is hidden on a real provider page, and the video element is
    provably still visible/functional.
- Keep the existing Windows WPF PoC (`MainWindow.xaml.cs` etc.) as reference/fallback until the
  MAUI Windows target is confirmed at full feature parity, then remove it to avoid maintaining two
  Windows hosts — don't delete it prematurely.

### 2. Settings/allowlist UI polish

Replace the current bare "Ad-Block: ON/OFF" + "Allowlist this site" buttons in the status bar with
a proper settings surface, per platform's native idiom (MAUI gives you Shell/flyout navigation for
this):
- A dedicated Settings page: master toggle, allowlist management (add/remove/view entries, not
  just a single "allowlist current site" action), filter-list status (rule counts, last updated,
  a manual "check for updates now" for filter lists), and app version + update-check status (see
  Task 3).
- A first-run experience: on first launch, briefly explain what the app does and that ad-blocking
  is active — this is a "why does this app exist" moment, keep it short.
- Persist everything through the existing [AdBlock/AppSettings.cs](AdBlock/AppSettings.cs) shape
  (extend it as needed — e.g. an `AutoUpdateEnabled` flag) rather than inventing a parallel
  settings mechanism.

### 3. Auto-update via GitHub Releases

- Add a portable (in `AdBlock/` or a new shared folder — keep it host-agnostic) `UpdateChecker`
  that calls the GitHub Releases API (`GET /repos/im-ashar/MoviesMafia/releases/latest`, no auth
  needed for a public repo's public releases) and compares its tag to the running app's version.
  Use `System.Net.Http`, handle rate limits/network failures gracefully (this must never crash or
  block startup — check in the background, surface a non-intrusive "update available" affordance).
- **Windows update flow:** download the new release asset (installer or self-contained exe),
  launch it, exit the current process. Confirm what asset naming/format you're producing in
  packaging (Task 4) and match it here.
- **Android update flow:** Android cannot silently replace a running APK. Realistic flow: detect a
  newer release, prompt the user, open the release's `.apk` download URL (or an in-app download +
  `ACTION_VIEW`/package-installer intent prompting the user through Android's normal APK-install
  UI, which requires the "install unknown apps" permission for your app's source — document this
  requirement clearly in the settings/first-run UI). Verify current Android APK-install-intent
  requirements against Android's current documentation; API/permission requirements around
  unknown-app installs have changed across Android versions — don't assume behavior from stale
  knowledge.
- **Signature consistency for Android:** decide now and document clearly — use ONE self-signed/
  debug keystore, generated once and reused for every build (checked into the repo or a
  documented private location — NOT regenerated per CI run), so that update-over-install doesn't
  fail with a signature mismatch. Flag this decision prominently since it's easy to get wrong
  silently (an update that "fails" with no clear error, only discovered by a real user later).

### 4. Packaging pipeline with a signing hook, unsigned by default

- Windows: keep/extend the existing self-contained single-file publish. Decide MSIX vs. plain exe
  installer (a plain self-contained exe, as already configured, is simplest and matches "direct
  download, no store" — MSIX without store distribution adds complexity for little benefit here;
  don't add it unless you have a specific reason).
- Android: produce a signed-with-the-reused-keystore (Task 3) release APK via
  `dotnet publish -f net10.0-android -c Release`.
- **The signing hook:** wire both platforms' signing step to read a cert/keystore path and
  password from an environment variable / MSBuild property with a documented name (e.g.
  `ADBLOCKAPP_WIN_CERT_PATH` / `ADBLOCKAPP_ANDROID_KEYSTORE_PATH` — pick sensible names and
  document them in README). If unset, fall back to unsigned (Windows) or the reused debug keystore
  (Android). This is the ONE place a real cert gets plugged in later — make it obvious and
  documented, don't bury it.
- Update README/HANDOFF with the exact commands to produce a release build for each platform, and
  where the output artifacts land, so they can be manually attached to a GitHub Release.

---

## Constraints & conventions (respect these)

- **Never break the web build.** Keep the shell (or its MAUI successor) out of `MoviesMafia.sln`
  and keep the `tools/**` `<Compile Remove>` exclusion in
  [MoviesMafia.csproj](../../MoviesMafia.csproj) accurate for wherever the new project(s) live.
  After your changes, verify BOTH build clean:
  `dotnet build MoviesMafia.csproj` (pass `TAILWIND_CLI=C:/Users/m.ashar/Downloads/tw.exe`) **and**
  every new/kept shell project.
- **Match existing code style**: heavy explanatory comments on the *why* (not the what), small
  focused methods/files, nullable enabled, implicit usings. Read a few existing files
  ([AdBlock/CosmeticInjector.cs](AdBlock/CosmeticInjector.cs) is a good example of the comment
  density expected) before writing new ones.
- **Verify platform APIs against current docs before relying on them — do not guess from general
  knowledge, especially for Android WebView and MAUI platform handlers.** This project has already
  caught multiple real bugs (over-blocking the provider's own script, confirming
  `NavigationStarting` is main-frame-only, confirming `AddScriptToExecuteOnDocumentCreatedAsync`'s
  cross-frame timing) precisely by checking docs and then proving behavior with a real test rather
  than assuming. Continue that pattern for every Android/MAUI-specific claim in this task list —
  several are flagged above with "verify" for exactly this reason.
- **Prove correctness with a live test on both platforms**, not just "it compiles" — a MAUI target
  compiling is not evidence the WebView interception hooks actually fire or fire early enough.
- **Dev environment tooling** (Git Bash has no coreutils/dotnet on PATH): dotnet is
  `/c/Program Files/dotnet/dotnet.exe`, git is `/c/Program Files/Git/cmd/git.exe`. SDKs 8.0.418 &
  10.0.301 installed — confirm the MAUI Android/Windows workloads are installed
  (`dotnet workload list` / `dotnet workload install maui`) before assuming they're available; if
  not, ask the user to install them (this may require Visual Studio installer components, not just
  a workload restore, for the Android SDK/emulator toolchain) since installing Android
  SDK/emulator tooling is a meaningful local environment change worth flagging rather than doing
  silently.
- **Don't strip provider revenue silently in a way that breaks playback.** Every change must be
  validated against a live watch page on both platforms before being called done.

## Definition of done

1. A MAUI app builds and runs on Windows and Android, sharing the unmodified `AdBlock/` engine.
2. Network blocking, popup/hijack suppression, and cosmetic filtering are proven working on BOTH
   platforms against a real provider watch page (not just "ported code that should work").
3. A proper settings UI (toggle, allowlist management, filter-list status, version/update status)
   replaces the bare buttons, with a short first-run explanation.
4. Auto-update checks GitHub Releases on both platforms and can walk the user through installing
   an update (direct exe relaunch on Windows; guided APK install on Android with a *stable* signing
   identity across versions).
5. Packaging produces unsigned/self-signed artifacts today, with one clearly documented hook per
   platform where a real signing certificate/keystore drops in later with no code changes.
6. The web app still builds clean; the `AdBlock/` engine remains host-agnostic (no WPF/WebView2/
   MAUI-specific types leak into it).
7. README/HANDOFF updated: MAUI architecture, per-platform verified behavior and any confirmed
   platform limitations (e.g. if Android genuinely cannot achieve the same cosmetic-injection
   timing guarantee as WebView2, say so explicitly rather than shipping a silent gap), exact
   release build commands, and the signing-hook variable names.
