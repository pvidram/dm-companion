using Bunit;
using DmCompanion.Core.Vault.Storage;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Platform;
using DmCompanion.Screens.Services;
using DmCompanion.Screens.Tests.Support;
using DmCompanion.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Screens.Tests;

public sealed class GlossaryLinksTests : BunitContext, IAsyncLifetime
{
    private const string NotePath = "Historia/Glosario.md";
    private const string Body = "---\ntype: lore\n---\nEl ogro cae [[Derribado]] por [[Bola de fuego|la bola]] de [[Nada de nada]].\n";

    private readonly TempVault _vault = TempVault.FromDemo();

    public GlossaryLinksTests()
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
        Services.AddGlossaryForTests();
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
    }

    Task IAsyncLifetime.InitializeAsync() => Services.GetRequiredService<VaultHost>().OpenAsync(_vault.Root);

    async Task IAsyncLifetime.DisposeAsync()
    {
        await Services.GetRequiredService<VaultHost>().DisposeAsync();
        await base.DisposeAsync();
        _vault.Dispose();
    }

    private ShellState Shell => Services.GetRequiredService<ShellState>();

    private async Task<IRenderedComponent<NoteBody>> RenderNoteAsync()
    {
        var host = Services.GetRequiredService<VaultHost>();
        await host.Current!.SaveAsync(NotePath, Body, WritePrecondition.Overwrite);
        var entity = host.Current!.Index.Resolve("Glosario")!;
        return Render<NoteBody>(p => p.Add(x => x.Entity, entity));
    }

    [Fact]
    public async Task Srd_link_is_not_rendered_as_broken()
    {
        var cut = await RenderNoteAsync();

        var classes = cut.FindAll("a.wikilink").Select(a => (a.TextContent, a.ClassList.Contains("srd"), a.ClassList.Contains("broken"))).ToList();

        classes.Should().Equal(("Derribado", true, false), ("la bola", true, false), ("Nada de nada", false, true));
    }

    [Fact]
    public async Task Clicking_an_srd_link_opens_the_glossary_entry()
    {
        var cut = await RenderNoteAsync();

        await cut.InvokeAsync(() => cut.Instance.OnWikilinkClicked("Derribado", false));

        (Shell.ActivePanel, Shell.SelectedGlossaryKey, Shell.NewNote).Should().Be((OverlayPanel.Glossary, "srd:estados/prone", null));
    }

    [Fact]
    public async Task Vault_note_wins_over_srd()
    {
        var host = Services.GetRequiredService<VaultHost>();
        await host.Current!.SaveAsync("Historia/Derribado.md", "---\ntype: lore\n---\nMi regla.\n", WritePrecondition.Overwrite);
        var cut = await RenderNoteAsync();

        await cut.InvokeAsync(() => cut.Instance.OnWikilinkClicked("Derribado", false));

        (cut.FindAll("a.wikilink")[0].ClassList.Contains("srd"), Shell.SelectedNotePath, Shell.SelectedGlossaryKey)
            .Should().Be((false, "Historia/Derribado.md", null));
    }
}
