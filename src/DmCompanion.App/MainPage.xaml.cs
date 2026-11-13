using DmCompanion.Screens.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DmCompanion.App;

public partial class MainPage : ContentPage
{
	public MainPage()
	{
		InitializeComponent();
	}

	// Los ficheros del vault (retratos, mapas) se sirven por /_vault/…; el resto de peticiones sigue su curso.
	private void OnWebResourceRequested(object? sender, WebViewWebResourceRequestedEventArgs e) =>
		Handler?.MauiContext?.Services.GetService<VaultFileRequestHandler>()?.Handle(e);

	// El botón atrás de Android pasa por Blazor para respetar el texto sin guardar y los diálogos abiertos.
	protected override bool OnBackButtonPressed()
	{
		_ = HandleBackAsync();
		return true;
	}

	private async Task HandleBackAsync()
	{
		var logger = Handler?.MauiContext?.Services.GetService<ILogger<MainPage>>();
		try
		{
			var handled = false;
			var dispatched = await blazorWebView.TryDispatchAsync(async sp =>
				handled = await sp.GetRequiredService<BackNavigation>().HandleBackAsync());
			if (!dispatched || !handled) MoveToBackground();
		}
		catch (Exception ex)
		{
			logger?.LogError(ex, "Fallo al gestionar el botón atrás.");
			try { MoveToBackground(); }
			catch (Exception inner) { logger?.LogError(inner, "Fallo al enviar la app a segundo plano."); }
		}
	}

	private static void MoveToBackground()
	{
#if ANDROID
		Platform.CurrentActivity?.MoveTaskToBack(true);
#endif
	}
}
