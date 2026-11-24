using DmCompanion.Core.Audio;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.Screens.Tests.Support;

internal static class AudioLibraryTestExtensions
{
    /// <summary>
    /// La biblioteca de audio se reconstruye en el pool al resolver el servicio por primera vez (lo hace el shell) y avisa con
    /// <c>Changed</c> desde otro hilo: ese re-render ocupa el dispatcher de bUnit y deja sin pintar las actualizaciones que la
    /// prueba dispara a la vez. Se resuelve y se espera aquí, con el vault ya abierto y antes de renderizar, para que nada
    /// del pool quede en vuelo.
    /// </summary>
    public static async Task WaitForAudioLibraryAsync(this IServiceProvider services, TimeSpan timeout)
    {
        var library = services.GetRequiredService<AudioLibraryService>();
        var deadline = DateTime.UtcNow + timeout;
        while (ReferenceEquals(library.Current, AudioLibrary.Empty))
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("La biblioteca de audio no terminó de cargarse.");
            await Task.Delay(2);
        }
    }
}
