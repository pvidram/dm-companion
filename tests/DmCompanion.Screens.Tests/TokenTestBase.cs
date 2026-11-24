using DmCompanion.Screens.Tests.Support;
using Bunit;
using DmCompanion.Core.Maps;
using DmCompanion.Screens.Components;
using DmCompanion.Core.Vault.Storage;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Platform;
using DmCompanion.Screens.Services;
using DmCompanion.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace DmCompanion.Screens.Tests;

/// <summary>Contexto común de las pruebas de tokens: vault temporal con un mapa abierto y los servicios del shell.</summary>
public abstract class TokenTestBase : BunitContext, IAsyncLifetime
{
    protected static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly TempVault _vault = TempVault.Empty();

    protected FakeTimeProvider Clock { get; } = new();

    protected TokenTestBase()
    {
        // Sin lienzo cargado el interop no llama a JS: el centro de la vista cae al del mapa.
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddDmCompanionUI();
        Services.AddSingleton<IAppPreferences>(new InMemoryAppPreferences());
        Services.AddSingleton<IVaultServiceFactory>(new FileSystemVaultServiceFactory(NullLoggerFactory.Instance));
        Services.AddSingleton<VaultHost>();
        // Reloj falso: con el real, el guardado diferido de MapSession (750 ms) dispara solo en las pruebas lentas y su re-render
        // desde el pool ocupa el dispatcher de bUnit a mitad de la prueba. Los guardados se hacen con Session.FlushAsync().
        Services.AddSingleton<TimeProvider>(Clock);
        Services.AddSingleton<MapSession>();
        Services.AddSingleton<TokenHitPoints>();
        Services.AddSingleton<CombatController>();
        Services.AddSingleton<VaultFileUrls>();
        Services.AddSingleton<NoteImageResolver>();
        Services.AddSingleton<PortraitUrlProvider>();
        Services.AddScoped<ShellState>();
        Services.AddGlossaryForTests();
        Services.AddSoundForTests();
        Services.AddScoped<MapCanvasInterop>();
        Services.AddScoped<TurnStripInterop>();
        Services.AddScoped<DragPanelInterop>();
    }

    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

    async Task IAsyncLifetime.DisposeAsync()
    {
        await Services.GetRequiredService<VaultHost>().DisposeAsync();
        await base.DisposeAsync();
        _vault.Dispose();
    }

    protected MapSession Session => Services.GetRequiredService<MapSession>();
    protected ShellState Shell => Services.GetRequiredService<ShellState>();
    protected VaultHost Host => Services.GetRequiredService<VaultHost>();
    protected IReadOnlyList<MapToken> Tokens => Session.Editor!.State.Tokens;

    /// <summary>
    /// Renderiza el shell y espera a que se drene el trabajo que el dispatcher tenía encolado hasta ese momento (una barrera: no
    /// garantiza que quede ocioso). El primer render deja trabajo encolado (renders en cascada, continuaciones asíncronas) que
    /// sigue corriendo en otro hilo cuando Render ya ha devuelto; una prueba que cambia el estado a continuación desde su propio
    /// hilo encola el re-render detrás y lee el DOM antes de que se pinte.
    /// </summary>
    protected async Task<IRenderedComponent<DmShell>> RenderShellAsync()
    {
        var cut = Render<DmShell>();
        await cut.InvokeAsync(() => { });
        return cut;
    }

    /// <summary>
    /// Cambia el estado de la app desde la prueba por el dispatcher del renderizador (como indica bUnit para cambios externos):
    /// el re-render que dispara el cambio queda hecho al volver, aunque otro hilo esté pintando en ese momento.
    /// </summary>
    protected Task OnDispatcherAsync(Action change) => Renderer.Dispatcher.InvokeAsync(change);

    /// <summary>El input o select de un campo del design system, localizado por su etiqueta visible.</summary>
    protected static AngleSharp.Dom.IElement Field(IRenderedComponent<Microsoft.AspNetCore.Components.IComponent> cut, string label) =>
        cut.FindAll("label.ds-field").First(l => l.QuerySelector(".ds-field__label")!.TextContent == label).QuerySelector("input,select")!;

    protected string PathOf(string relativePath) => _vault.PathOf(relativePath);

    protected void WriteNote(string relativePath, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_vault.PathOf(relativePath))!);
        File.WriteAllText(_vault.PathOf(relativePath), text);
    }

    /// <summary>Abre el vault y el mapa de ejemplo (con Lyra) con las notas extra que pida la prueba.</summary>
    protected async Task OpenMapAsync(MapState? state = null, string? rawState = null, Action? extraNotes = null)
    {
        MapTestVault.WriteImageMap(_vault, state ?? MapState.Empty, rawState);
        extraNotes?.Invoke();
        await Host.OpenAsync(_vault.Root);
        await Session.OpenAsync(MapTestVault.MapNote);
        await Services.WaitForAudioLibraryAsync(Timeout);
    }

    protected static MapToken NoteToken(string id, string name, string source, TokenSide side = TokenSide.Pc, double x = 35, double y = 35) =>
        new(id, name, source, side, 1, x, y, null, null);

    protected static MapToken GenericToken(string id, string name, int? ac = null, int? hp = null, TokenSide side = TokenSide.Enemy) =>
        new(id, name, null, side, 1, 35, 35, ac, hp);
}
