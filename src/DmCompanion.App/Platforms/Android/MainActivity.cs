using Android.App;
using Android.Content.PM;
using Android.OS;
using DmCompanion.Screens.Services;

namespace DmCompanion.App;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ScreenOrientation = ScreenOrientation.SensorLandscape,ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
	// Cerrar la app (spec §6.1: la TV queda encendida «hasta cerrar la app») apaga la TV: el proceso y sus singletons pueden
	// sobrevivir a la actividad. Solo si termina de verdad (IsFinishing): el botón atrás en la raíz la manda a segundo plano
	// (MainPage) y apagar la pantalla tampoco la destruye; ahí la TV sigue activada y se reconecta al volver.
	protected override void OnDestroy()
	{
		if (IsFinishing) _ = IPlatformApplication.Current?.Services.GetService<TvSession>()?.DisableOnExitAsync();
		base.OnDestroy();
	}
}
