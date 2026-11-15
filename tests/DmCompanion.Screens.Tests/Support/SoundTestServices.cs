using DmCompanion.Screens.Platform;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.Screens.Tests.Support;

internal static class SoundTestServices
{
    /// <summary>Servicios de sonido con reproductor falso; necesita VaultHost y TimeProvider ya registrados.</summary>
    public static IServiceCollection AddSoundForTests(this IServiceCollection services)
    {
        services.AddSingleton<FakeAudioPlayer>();
        services.AddSingleton<IAudioPlayer>(sp => sp.GetRequiredService<FakeAudioPlayer>());
        services.AddSingleton<AudioLibraryService>();
        services.AddSingleton<AudioStore>();
        services.AddScoped<AudioMixer>();
        return services;
    }
}
