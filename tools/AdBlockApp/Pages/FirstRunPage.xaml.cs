using AdBlockCore;

namespace AdBlockApp.Pages;

/// <summary>
/// Shown exactly once, before the first navigation to <see cref="BrowserPage"/> — see
/// HANDOFF.md Task 2's "first-run experience" requirement. Reads/writes the SAME
/// <see cref="AppSettings"/> file <see cref="Services.AdBlockRuntime"/> uses (via its own
/// short-lived load) rather than a parallel "have I shown this" mechanism, per HANDOFF's
/// instruction to extend AppSettings instead of inventing a second settings store.
/// </summary>
public partial class FirstRunPage : ContentPage
{
    public FirstRunPage()
    {
        InitializeComponent();
    }

    private async void OnContinueClicked(object? sender, EventArgs e)
    {
        var settings = AppSettings.Load(System.IO.Path.Combine(Microsoft.Maui.Storage.FileSystem.AppDataDirectory, "settings.json"));
        settings.FirstRunCompleted = true;
        settings.Save();

        await Shell.Current.GoToAsync($"//{nameof(BrowserPage)}");
    }
}
