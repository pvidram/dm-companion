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
        var published = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnChanged(object? sender, EventArgs e) => published.TrySetResult();
        library.Changed += OnChanged;
        try
        {
            // Se suscribe antes de mirar Current: lo que ya se publicó antes de suscribirse se ve en Current (cada publicación
            // es una instancia nueva); lo que se publique después llega por el evento.
            if (ReferenceEquals(library.Current, AudioLibrary.Empty)) await published.Task.WaitAsync(timeout);
        }
        catch (TimeoutException)
        {
            throw new TimeoutException("La biblioteca de audio no terminó de cargarse.");
        }
        finally
        {
            library.Changed -= OnChanged;
        }
    }
}
