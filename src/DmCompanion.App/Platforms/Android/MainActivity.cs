using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using DmCompanion.Screens.Platform;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.Logging;

namespace DmCompanion.App;

// SingleTask: un «Compartir» con la app abierta llega a OnNewIntent en la misma actividad (y en el mismo Blazor) en vez de abrir otra.
[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTask, ScreenOrientation = ScreenOrientation.SensorLandscape, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
[IntentFilter(new[] { Intent.ActionSend }, Categories = new[] { Intent.CategoryDefault }, DataMimeType = "image/*")]
[IntentFilter(new[] { Intent.ActionSendMultiple }, Categories = new[] { Intent.CategoryDefault }, DataMimeType = "image/*")]
public class MainActivity : MauiAppCompatActivity
{
	protected override void OnCreate(Bundle? savedInstanceState)
	{
		base.OnCreate(savedInstanceState);
		// Con savedInstanceState != null la actividad se recrea: no hay que repetir el «Compartir» ni vaciar lo pendiente.
		if (savedInstanceState is not null) return;
		var share = IPlatformApplication.Current?.Services.GetService<IncomingShare>();
		if (share is { HasPending: false }) share.CleanTemp();
		ReceiveShare(Intent);
	}

	protected override void OnNewIntent(Intent? intent)
	{
		base.OnNewIntent(intent);
		Intent = intent; // equivale a SetIntent(intent): getIntent() devuelve el último
		ReceiveShare(intent);
	}

	private void ReceiveShare(Intent? intent)
	{
		if (intent is null || !ShareIntentReader.IsShare(intent)) return;
		// Abrir la app desde recientes puede reentregar el intent original: no se reimporta.
		if (intent.Flags.HasFlag(ActivityFlags.LaunchedFromHistory)) return;
		var services = IPlatformApplication.Current?.Services;
		if (services?.GetService<IncomingShare>() is not { } share || services.GetService<IShareInbox>() is not { } inbox) return;
		var logger = services.GetRequiredService<ILogger<MainActivity>>();
		// HandleAsync captura sus errores; aquí no hay nada que esperar.
		_ = ShareIntentReader.HandleAsync(this, intent, share, inbox, logger);
	}

	// Cerrar la app (spec §6.1: la TV queda encendida «hasta cerrar la app») apaga la TV: el proceso y sus singletons pueden
	// sobrevivir a la actividad. Solo si termina de verdad (IsFinishing): el botón atrás en la raíz la manda a segundo plano
	// (MainPage) y apagar la pantalla tampoco la destruye; ahí la TV sigue activada y se reconecta al volver.
	protected override void OnDestroy()
	{
		if (IsFinishing) _ = IPlatformApplication.Current?.Services.GetService<TvSession>()?.DisableOnExitAsync();
		base.OnDestroy();
	}
}
