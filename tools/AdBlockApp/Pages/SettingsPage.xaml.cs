using System.Collections.ObjectModel;
using AdBlockApp.Services;
using AdBlockCore;

namespace AdBlockApp.Pages;

/// <summary>
/// Replaces AdBlockShell's bare "Ad-Block: ON/OFF" + "Allowlist this site" toolbar buttons with
/// a proper settings surface — see HANDOFF.md Task 2. Reads/writes the SAME
/// <see cref="AdBlockRuntime"/> singleton <see cref="BrowserPage"/> filters with (injected via
/// DI, see MauiProgram), so toggling ad-block here takes effect on the live browser the next
/// time it evaluates a request/re-registers cosmetics — no separate "apply" step needed.
/// </summary>
public partial class SettingsPage : ContentPage
{
    private readonly AdBlockRuntime _runtime;
    private readonly UpdateChecker _updateChecker;
    private readonly ObservableCollection<string> _allowlist = new();

    public SettingsPage(AdBlockRuntime runtime, UpdateChecker updateChecker)
    {
        _runtime = runtime;
        _updateChecker = updateChecker;
        InitializeComponent();
        AllowlistView.ItemsSource = _allowlist;
        Loaded += OnPageLoaded;
    }

    private async void OnPageLoaded(object? sender, EventArgs e)
    {
        // SettingsPage can be opened before BrowserPage finishes its first InitializeAsync
        // (e.g. the user taps Settings immediately) — InitializeAsync is idempotent (see its
        // doc comment), so awaiting it here just joins whichever call started first.
        await _runtime.InitializeAsync();

        AdBlockSwitch.IsToggled = _runtime.Settings.AdBlockEnabled;
        AutoUpdateSwitch.IsToggled = _runtime.Settings.AutoUpdateEnabled;
        RefreshAllowlist();
        RefreshFilterStatus();
        VersionLabel.Text = $"Version {AppInfo.Current.VersionString}";

        _runtime.StatusChanged += _ => MainThread.BeginInvokeOnMainThread(RefreshFilterStatus);
    }

    private void RefreshAllowlist()
    {
        _allowlist.Clear();
        foreach (var host in _runtime.Settings.AllowlistedHosts) _allowlist.Add(host);
    }

    private void RefreshFilterStatus()
    {
        var network = _runtime.Network;
        FilterStatusLabel.Text = network is null
            ? "Loading…"
            : $"{network.RuleCount:N0} network rules · {_runtime.Cosmetic?.GenericHideSelectors.Count ?? 0:N0} cosmetic selectors";
    }

    private void OnAdBlockToggled(object? sender, ToggledEventArgs e)
    {
        _runtime.Settings.AdBlockEnabled = e.Value;
        _runtime.Settings.Save();
    }

    private void OnAutoUpdateToggled(object? sender, ToggledEventArgs e)
    {
        _runtime.Settings.AutoUpdateEnabled = e.Value;
        _runtime.Settings.Save();
    }

    private async void OnCheckFiltersClicked(object? sender, EventArgs e)
    {
        CheckFiltersButton.IsEnabled = false;
        try { await _runtime.ForceRefreshAsync(); }
        finally { CheckFiltersButton.IsEnabled = true; }
    }

    private void OnAddAllowlistEntryClicked(object? sender, EventArgs e)
    {
        var host = NewAllowlistEntry.Text?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(host)) return;

        if (!_runtime.Settings.AllowlistedHosts.Contains(host))
        {
            _runtime.Settings.AllowlistedHosts.Add(host);
            _runtime.Settings.Save();
            _allowlist.Add(host);
        }
        NewAllowlistEntry.Text = "";
    }

    private void OnRemoveAllowlistEntryClicked(object? sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is not string host) return;

        _runtime.Settings.AllowlistedHosts.Remove(host);
        _runtime.Settings.Save();
        _allowlist.Remove(host);
    }

    private async void OnCheckUpdateClicked(object? sender, EventArgs e)
    {
        CheckUpdateButton.IsEnabled = false;
        UpdateStatusLabel.Text = "Checking…";
        try
        {
            var release = await _updateChecker.TryGetLatestReleaseAsync();
            if (release is null)
            {
                UpdateStatusLabel.Text = "Couldn't check for updates (offline or rate-limited) — try again later.";
                return;
            }

            UpdateStatusLabel.Text = UpdateChecker.IsNewer(release.TagName, AppInfo.Current.VersionString)
                ? $"Update available: {release.TagName} — tap below to install."
                : $"Up to date ({AppInfo.Current.VersionString}).";

            if (UpdateChecker.IsNewer(release.TagName, AppInfo.Current.VersionString))
                await PromptInstallUpdateAsync(release);
        }
        finally
        {
            CheckUpdateButton.IsEnabled = true;
        }
    }

    /// <summary>Implemented per-platform (<c>Platforms/Windows/SettingsPage.Windows.cs</c> /
    /// <c>Platforms/Android/SettingsPage.Android.cs</c>) — see HANDOFF.md Task 3: Windows
    /// downloads the new exe/installer and relaunches; Android hands the user to the system
    /// package installer via an ACTION_VIEW intent, since it cannot self-update silently.</summary>
    private partial Task PromptInstallUpdateAsync(ReleaseInfo release);
}
