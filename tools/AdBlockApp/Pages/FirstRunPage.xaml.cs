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

        // Pop back to the BrowserPage this page was pushed on top of (see App.CreateWindow). ".."
        // is relative navigation — the counterpart to the relative push there; absolute routing
        // ("//BrowserPage") would crash the same way the old first-run navigation did on Android.
        await Shell.Current.GoToAsync("..");
    }
}
