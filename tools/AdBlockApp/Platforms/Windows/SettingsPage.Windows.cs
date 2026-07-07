using AdBlockCore;

namespace AdBlockApp.Pages;

/// <summary>
/// Windows half of the update flow. Unlike Android, we do NOT try to self-install: the MAUI
/// Windows app publishes as a ~500-file WindowsAppSDK folder (PublishSingleFile is unsupported
/// there and silently ignored), so there is no single .exe to swap in-place and no safe way to
/// replace a running multi-file folder from within the running process. Instead we hand the user
/// to the GitHub release page, where the Windows asset is a .zip they download and extract over
/// their existing install. Simple and robust; see the release workflow (.github/workflows/
/// release-apps.yml) which produces that zip, and the app README's "Auto-update" section.
/// </summary>
public partial class SettingsPage
{
    private partial async Task PromptInstallUpdateAsync(ReleaseInfo release)
    {
        var confirmed = await DisplayAlert("Update available",
            $"MoviesMafia {release.TagName} is available. Open the download page? " +
            "Extract the new version over your current install to update.",
            "Open download page", "Not now");
        if (!confirmed) return;

        // Prefer the release's Windows .zip asset if present; otherwise the human release page.
        var zip = release.Assets.FirstOrDefault(a =>
            a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
            a.Name.Contains("win", StringComparison.OrdinalIgnoreCase));
        var target = zip?.BrowserDownloadUrl ?? release.HtmlUrl;

        if (Uri.TryCreate(target, UriKind.Absolute, out var uri))
            await Launcher.Default.OpenAsync(uri);

        UpdateStatusLabel.Text = "Opened the download page in your browser.";
    }
}
