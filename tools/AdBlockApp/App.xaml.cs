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
			window.Created += async (_, _) => await Shell.Current.GoToAsync($"//{nameof(Pages.FirstRunPage)}");
		}

		return window;
	}
}