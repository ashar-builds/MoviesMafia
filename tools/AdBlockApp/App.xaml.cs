namespace AdBlockApp;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState) =>
		// Straight to the BrowserPage (AppShell's only ShellContent). The app opens on a loading
		// indicator that stays up until the site finishes loading (see BrowserPage) — that IS the
		// launch experience now; the old first-run explainer page was removed in favor of it.
		new Window(new AppShell());
}