using AdBlockApp.Pages;
using AdBlockApp.Services;
using AdBlockCore;
using Microsoft.Extensions.Logging;

namespace AdBlockApp;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		// One AdBlockRuntime per app run, shared by BrowserPage (does the actual filtering) and
		// SettingsPage (reads/writes the same Settings/engines live) — see AdBlockRuntime's doc
		// comment for why this must be a singleton, not a per-page instance.
		builder.Services.AddSingleton<AdBlockRuntime>();
		builder.Services.AddSingleton<UpdateChecker>();
		builder.Services.AddTransient<FirstRunPage>();
		builder.Services.AddTransient<BrowserPage>();
		builder.Services.AddTransient<SettingsPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
