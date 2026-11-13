using DmCompanion.Screens.Services;
using Microsoft.Extensions.Logging;

namespace DmCompanion.App;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(new MainPage()) { Title = "DM Companion" };

		// Stopped llega en Android desde OnStop, cuando la app pasa a segundo plano. Es el último momento
		// seguro para guardar: desde recientes el sistema puede matar el proceso sin más eventos.
		// MapSession es singleton: se resuelve del contenedor raíz para vaciar el guardado diferido.
		var services = activationState?.Context.Services;
		var session = services?.GetService<MapSession>();
		if (session is not null)
		{
			var logger = services!.GetService<ILogger<App>>();
			window.Stopped += (_, _) => _ = FlushMapAsync(session, logger);
		}

		return window;
	}

	// Fire-and-forget observado: FlushAsync ya convierte los fallos de IO en aviso del mapa.
	private static async Task FlushMapAsync(MapSession session, ILogger? logger)
	{
		try
		{
			await session.FlushAsync();
		}
		catch (Exception ex)
		{
			logger?.LogError("Fallo inesperado al guardar el mapa al pasar a segundo plano ({ExceptionType}).", ex.GetType().FullName);
		}
	}
}
