using Android.App;
using Android.Content;
using Android.Provider;
using AdBlockCore;
using Uri = Android.Net.Uri;
using AndroidXFileProvider = AndroidX.Core.Content.FileProvider;

namespace AdBlockApp.Pages;

/// <summary>
/// Android half of the update-install flow (see HANDOFF.md Task 3). Android cannot silently
/// self-update: this downloads the release APK into the app's external-files "updates"
/// subdirectory (matches <c>Resources/xml/file_paths.xml</c>'s <c>external-files-path</c>
/// entry), then hands the user to the SYSTEM package installer via an
/// <c>Intent.ActionView</c> on a <c>content://</c> URI — the normal Android sideload-install UI,
/// which requires the per-app "Install unknown apps" special permission
/// (<c>REQUEST_INSTALL_PACKAGES</c>, Android 8.0+) to be granted first.
///
/// Signature-consistency requirement (HANDOFF.md Task 3): this only succeeds if the downloaded
/// APK is signed with the SAME key as the currently-installed one — see README's "Signing" section
/// for the reused debug/self-signed keystore this project commits to for exactly that reason.
/// </summary>
public partial class SettingsPage
{
    private partial async Task PromptInstallUpdateAsync(ReleaseInfo release)
    {
        var asset = release.Assets.FirstOrDefault(a => a.Name.EndsWith(".apk", StringComparison.OrdinalIgnoreCase));
        if (asset is null)
        {
            UpdateStatusLabel.Text = $"Update {release.TagName} available, but no .apk asset was found on the release.";
            return;
        }

        var confirmed = await DisplayAlertAsync("Update available",
            $"MoviesMafia {release.TagName} is available. Download it now? You'll be asked to confirm the install afterward.",
            "Download", "Not now");
        if (!confirmed) return;

        var context = Android.App.Application.Context;

        // "Install unknown apps" is a per-app special permission (API 26+), not a runtime
        // dialog — the ONLY way to grant it is the user visiting this Settings screen
        // themselves. We can't skip this with a permission request; the best we can do is
        // send them straight to the right screen. Document this prominently — a silent no-op
        // install failure here is exactly the "discovered by a real user later" trap HANDOFF.md
        // warns about.
        if (!context.PackageManager!.CanRequestPackageInstalls())
        {
            UpdateStatusLabel.Text = "Enable \"Allow from this source\" on the next screen, then try the update again.";
            var settingsIntent = new Intent(Settings.ActionManageUnknownAppSources,
                Uri.Parse($"package:{context.PackageName}"));
            settingsIntent.AddFlags(ActivityFlags.NewTask);
            context.StartActivity(settingsIntent);
            return;
        }

        UpdateStatusLabel.Text = "Downloading update…";
        var updatesDir = new Java.IO.File(context.GetExternalFilesDir(null), "updates");
        updatesDir.Mkdirs();
        var apkFile = new Java.IO.File(updatesDir, asset.Name);

        using var http = new HttpClient();
        await using (var stream = await http.GetStreamAsync(asset.BrowserDownloadUrl))
        await using (var file = System.IO.File.Create(apkFile.AbsolutePath))
        {
            await stream.CopyToAsync(file);
        }

        // FileProvider authority MUST match AndroidManifest.xml's ${applicationId}.fileprovider
        // exactly, or GetUriForFile throws IllegalArgumentException at runtime.
        var apkUri = AndroidXFileProvider.GetUriForFile(context, $"{context.PackageName}.fileprovider", apkFile);

        var installIntent = new Intent(Intent.ActionView);
        installIntent.SetDataAndType(apkUri, "application/vnd.android.package-archive");
        installIntent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.NewTask);
        context.StartActivity(installIntent);

        UpdateStatusLabel.Text = "Follow the system install prompt to finish updating.";
    }
}
