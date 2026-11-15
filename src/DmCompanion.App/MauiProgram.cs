using DmCompanion.Core.Vault.Storage;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Platform;
using DmCompanion.Screens.Services;
using DmCompanion.UI;
using Microsoft.Extensions.Logging;

namespace DmCompanion.App;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>();

		builder.Services.AddMauiBlazorWebView();
		builder.Services.AddDmCompanionUI();
		builder.Services.AddSingleton<IVaultServiceFactory, FileSystemVaultServiceFactory>();
		builder.Services.AddSingleton<IFolderBrowser, DirectoryBrowser>();
		builder.Services.AddSingleton<IStorageAccess, AndroidStorageAccess>();
		builder.Services.AddSingleton<IAppPreferences, MauiAppPreferences>();
		builder.Services.AddSingleton<VaultHost>();
		builder.Services.AddSingleton<MapSession>();
		builder.Services.AddSingleton<TokenHitPoints>();
		builder.Services.AddSingleton<CombatController>();
		builder.Services.AddSingleton<MapLibrary>();
		builder.Services.AddSingleton<IImagePicker, MauiImagePicker>();
		builder.Services.AddScoped<ShellState>();
		builder.Services.AddScoped<WikilinkInterop>();
		builder.Services.AddScoped<MapCanvasInterop>();
		builder.Services.AddScoped<TurnStripInterop>();
		builder.Services.AddScoped<DragPanelInterop>();
		builder.Services.AddScoped<BackNavigation>();
		builder.Services.AddSingleton<VaultFileUrls>();
		builder.Services.AddSingleton<VaultFileRequestHandler>();
		builder.Services.AddSingleton<PortraitUrlProvider>();

		// Release también necesita sink: sin él los errores no llegan a `adb logcat -s DmCompanion`.
		builder.Logging.SetMinimumLevel(LogLevel.Information);
#if ANDROID
		builder.Logging.AddProvider(new AndroidLogcatLoggerProvider());
#endif

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
