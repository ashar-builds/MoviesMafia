using AdBlockCore;

namespace AdBlockApp;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(new AppShell());

		// FirstRunCompleted lives in the same AppSettings.json AdBlockRuntime reads later — a
		// direct AppSettings.Load here (synchronous, tiny file) is simpler than plumbing this
		// one boolean through DI before the Shell even exists. See HANDOFF.md Task 2's
		// first-run requirement.
		var settings = AppSettings.Load(Path.Combine(Microsoft.Maui.Storage.FileSystem.AppDataDirectory, "settings.json"));
		if (!settings.FirstRunCompleted)
		{
			// RELATIVE route (no "//"): push FirstRunPage on top of the default BrowserPage.
			// Absolute routing ("//FirstRunPage") to a Routing.RegisterRoute'd *global* route is
			// rejected by MAUI's Android Shell backend ("Global routes currently cannot be the
			// only page on the stack") and hard-crashes on the FIRST launch — WinUI's backend
			// happened to tolerate it, so this only ever surfaced on Android. Pushing relatively
			// keeps BrowserPage as the stack root (so it's not "the only page"), and lets it warm
			// the filter cache behind the explainer; FirstRunPage pops back to it on Continue.
			window.Created += async (_, _) => await Shell.Current.GoToAsync(nameof(Pages.FirstRunPage));
		}

		return window;
	}
}