using DmCompanion.Screens.Tests.Support;
using Bunit;
using DmCompanion.Core.Maps;
using DmCompanion.Core.Vault.Storage;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Platform;
using DmCompanion.Screens.Services;
using DmCompanion.UI;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Screens.Tests;

public abstract class MapsTestContext : BunitContext, IAsyncLifetime
{
    protected static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);
    private readonly TempVault _vault = TempVault.Empty();

    protected MapsTestContext()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddDmCompanionUI();
        Services.AddSingleton<IAppPreferences>(new InMemoryAppPreferences());
        Services.AddSingleton<IVaultServiceFactory>(new FileSystemVaultServiceFactory(NullLoggerFactory.Instance));
        Services.AddSingleton<VaultHost>();
        Services.AddSingleton<VaultFileUrls>();
        Services.AddSingleton<NoteImageResolver>();
        Services.AddSingleton<PortraitUrlProvider>();
        Services.AddScoped<ShellState>();
        Services.AddSoundForTests();
        Services.AddSingleton(TimeProvider.System);
        Services.AddSingleton<MapSession>();
        Services.AddSingleton<TokenHitPoints>();
        Services.AddSingleton<CombatController>();
        Services.AddSingleton<MapLibrary>();
        Services.AddScoped<MapCanvasInterop>();
        Services.AddScoped<TurnStripInterop>();
        Services.AddScoped<DragPanelInterop>();
        Services.AddScoped<WikilinkInterop>();
        Configure();

        Write("Mapas/Cueva.md", "---\ntype: map\nname: Cueva\n---\n");
        Write("Mapas/Faro.md", "---\ntype: map\nname: Faro\nimage: Mapas/Faro.png\n---\n");
        Write("Mapas/Faro.png", "png");
        Write("Mapas/suelto.png", "png");
        Write("Historia/Pista.md", "---\ntype: note\nname: Pista\n---\nVe a [[Cueva]].\n");
    }

    /// <summary>Servicios extra de cada clase de tests; se registran antes de resolver nada.</summary>
    protected virtual void Configure() { }

    Task IAsyncLifetime.InitializeAsync() => Services.GetRequiredService<VaultHost>().OpenAsync(_vault.Root);

    async Task IAsyncLifetime.DisposeAsync()
    {
        await Services.GetRequiredService<VaultHost>().DisposeAsync();
        await base.DisposeAsync();
        _vault.Dispose();
    }

    protected ShellState Shell => Services.GetRequiredService<ShellState>();
    protected MapSession Session => Services.GetRequiredService<MapSession>();

    protected string PathOf(string path) => _vault.PathOf(path);

    protected void Write(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_vault.PathOf(path))!);
        File.WriteAllText(_vault.PathOf(path), text);
    }

    protected IRenderedComponent<DmShell> RenderOpenMaps()
    {
        var cut = Render<DmShell>();
        cut.InvokeAsync(() => Shell.Toggle(OverlayPanel.Maps)).GetAwaiter().GetResult();
        cut.WaitForAssertion(() => cut.FindAll("[data-testid=map-row]").Should().HaveCount(2), Wait);
        return cut;
    }

    protected static string OpenDialogText(IRenderedComponent<DmShell> cut) =>
        string.Concat(cut.FindAll(".ds-dialog-backdrop:not([hidden])").Select(d => d.TextContent));

    protected static AngleSharp.Dom.IElement OpenDialog(IRenderedComponent<DmShell> cut) =>
        cut.Find(".ds-dialog-backdrop:not([hidden]) .ds-dialog");
}
