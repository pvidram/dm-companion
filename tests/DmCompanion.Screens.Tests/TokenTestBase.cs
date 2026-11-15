using Bunit;
using DmCompanion.Core.Maps;
using DmCompanion.Core.Vault.Storage;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Platform;
using DmCompanion.Screens.Services;
using DmCompanion.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Screens.Tests;

/// <summary>Contexto común de las pruebas de tokens: vault temporal con un mapa abierto y los servicios del shell.</summary>
public abstract class TokenTestBase : BunitContext, IAsyncLifetime
{
    protected static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly TempVault _vault = TempVault.Empty();

    protected TokenTestBase()
    {
        // Sin lienzo cargado el interop no llama a JS: el centro de la vista cae al del mapa.
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddDmCompanionUI();
        Services.AddSingleton<IAppPreferences>(new InMemoryAppPreferences());
        Services.AddSingleton<IVaultServiceFactory>(new FileSystemVaultServiceFactory(NullLoggerFactory.Instance));
        Services.AddSingleton<VaultHost>();
        Services.AddSingleton(TimeProvider.System);
        Services.AddSingleton<MapSession>();
        Services.AddSingleton<TokenHitPoints>();
        Services.AddSingleton<CombatController>();
        Services.AddSingleton<VaultFileUrls>();
        Services.AddSingleton<PortraitUrlProvider>();
        Services.AddScoped<ShellState>();
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
    }

    protected static MapToken NoteToken(string id, string name, string source, TokenSide side = TokenSide.Pc, double x = 35, double y = 35) =>
        new(id, name, source, side, 1, x, y, null, null);

    protected static MapToken GenericToken(string id, string name, int? ac = null, int? hp = null, TokenSide side = TokenSide.Enemy) =>
        new(id, name, null, side, 1, 35, 35, ac, hp);
}
