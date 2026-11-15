using DmCompanion.Screens.Tests.Support;
using Bunit;
using DmCompanion.Core.Vault.Storage;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Platform;
using DmCompanion.Screens.Services;
using DmCompanion.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Screens.Tests;

public sealed class ConflictTests : BunitContext, IAsyncLifetime
{
    private const string Original = "Lugares/Puerto Sal.md";
    private const string Copy = "Lugares/Puerto Sal.sync-conflict-20261103-101500-ABCDEF1.md";
    private const string CopyText = "Versión editada en el PC";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly TempVault _vault = TempVault.FromDemo();

    public ConflictTests()
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
    }

    Task IAsyncLifetime.InitializeAsync() => Services.GetRequiredService<VaultHost>().OpenAsync(_vault.Root);

    async Task IAsyncLifetime.DisposeAsync()
    {
        await Services.GetRequiredService<VaultHost>().DisposeAsync();
        await base.DisposeAsync();
        _vault.Dispose();
    }

    private ShellState Shell => Services.GetRequiredService<ShellState>();

    private static AngleSharp.Dom.IElement Resolver(IRenderedComponent<DmShell> cut) =>
        cut.FindAll(".ds-dialog").First(d => d.TextContent.Contains("Conflictos de sincronización"));

    // Siempre vía cut.FindAll: son elementos refrescables, así un re-render entre la búsqueda y el clic no deja un manejador obsoleto.
    private static AngleSharp.Dom.IElement ResolverButton(IRenderedComponent<DmShell> cut, string text) =>
        cut.FindAll(".ds-dialog button").First(b => b.TextContent.Trim() == text);

    private IRenderedComponent<DmShell> RenderResolver()
    {
        var cut = Render<DmShell>();
        cut.FindAll("button").First(b => b.TextContent.Trim() == "Revisar").Click();
        cut.WaitForAssertion(() => Resolver(cut).TextContent.Should().Contain(CopyText), Timeout);
        return cut;
    }

    private string Read(string relativePath) => File.ReadAllText(_vault.PathOf(relativePath));

    [Fact]
    public void The_strip_shows_a_notice_for_the_demo_conflict()
    {
        var cut = Render<DmShell>();

        cut.Find("header.ds-frame__strip").TextContent.Should().Contain("1 conflicto de sincronización");
    }

    [Fact]
    public void Review_opens_the_resolver_with_both_versions()
    {
        var cut = RenderResolver();

        Resolver(cut).QuerySelectorAll("pre").Select(p => p.TextContent).Should().SatisfyRespectively(
            current => current.Should().Contain("Ciudad portuaria"),
            copy => copy.Should().Contain(CopyText));
    }

    [Fact]
    public void Keeping_the_copy_writes_its_text_to_the_original_and_the_notice_disappears()
    {
        var cut = RenderResolver();

        ResolverButton(cut, "Quedarme con la copia").Click();

        cut.WaitForAssertion(() =>
        {
            cut.Find("header.ds-frame__strip").TextContent.Should().NotContain("conflicto");
            Read(Original).Should().Contain(CopyText);
            File.Exists(_vault.PathOf(Copy)).Should().BeFalse();
            Shell.ConflictsOpen.Should().BeFalse();
        }, Timeout);
    }

    [Fact]
    public void Keeping_the_current_version_deletes_only_the_copy()
    {
        var cut = RenderResolver();

        ResolverButton(cut, "Quedarme con la actual").Click();

        cut.WaitForAssertion(() =>
            (File.Exists(_vault.PathOf(Copy)), Read(Original).Contains("Ciudad portuaria")).Should().Be((false, true)), Timeout);
    }

    [Fact]
    public void Original_modified_after_reading_it_is_reported_and_nothing_is_deleted()
    {
        var cut = RenderResolver();
        File.WriteAllText(_vault.PathOf(Original), "cambio externo");
        File.SetLastWriteTimeUtc(_vault.PathOf(Original), DateTime.UtcNow.AddMinutes(5));

        ResolverButton(cut, "Quedarme con la copia").Click();

        cut.WaitForAssertion(() =>
        {
            Resolver(cut).TextContent.Should().Contain("La versión actual ha cambiado mientras la mirabas");
            (Read(Original), File.Exists(_vault.PathOf(Copy))).Should().Be(("cambio externo", true));
            Resolver(cut).QuerySelectorAll("pre").First().TextContent.Should().Contain("cambio externo");
        }, Timeout);
    }

    [Fact]
    public void Original_deleted_after_reading_it_disables_keeping_the_current_version()
    {
        var cut = RenderResolver();
        File.Delete(_vault.PathOf(Original));

        ResolverButton(cut, "Quedarme con la actual").Click();

        cut.WaitForAssertion(() =>
        {
            File.Exists(_vault.PathOf(Copy)).Should().BeTrue();
            ResolverButton(cut, "Quedarme con la actual").HasAttribute("disabled").Should().BeTrue();
            Resolver(cut).TextContent.Should().Contain("(la versión actual ya no existe)");
        }, Timeout);
    }

    [Fact]
    public void Original_missing_when_opening_shows_it_and_disables_keeping_the_current_version()
    {
        File.Delete(_vault.PathOf(Original));
        var cut = RenderResolver();

        (Resolver(cut).TextContent.Contains("(la versión actual ya no existe)"), ResolverButton(cut, "Quedarme con la actual").HasAttribute("disabled"))
            .Should().Be((true, true));
    }

    [Fact]
    public async Task Keeping_the_copy_of_the_note_being_edited_asks_first_when_there_is_unsaved_text()
    {
        var cut = Render<DmShell>();
        await cut.InvokeAsync(() => Shell.EditNote(Original));
        cut.WaitForAssertion(() => cut.Find("textarea"), Timeout);
        cut.Find("textarea").Input("sin guardar");
        cut.WaitForAssertion(() => Shell.EditorDirty.Should().BeTrue(), Timeout);
        cut.FindAll("button").First(b => b.TextContent.Trim() == "Revisar").Click();
        cut.WaitForAssertion(() => Resolver(cut).TextContent.Should().Contain(CopyText), Timeout);

        ResolverButton(cut, "Quedarme con la copia").Click();

        cut.WaitForAssertion(() =>
            (Shell.ActiveEditorDialog, Read(Original).Contains(CopyText), cut.Find("textarea").GetAttribute("value"))
                .Should().Be((EditorDialog.ConfirmClose, false, "sin guardar")), Timeout);
    }

    [Fact]
    public async Task Confirming_the_discard_closes_the_editor_and_then_keeps_the_copy()
    {
        var cut = Render<DmShell>();
        await cut.InvokeAsync(() => Shell.EditNote(Original));
        cut.WaitForAssertion(() => cut.Find("textarea"), Timeout);
        cut.Find("textarea").Input("sin guardar");
        cut.WaitForAssertion(() => Shell.EditorDirty.Should().BeTrue(), Timeout);
        cut.FindAll("button").First(b => b.TextContent.Trim() == "Revisar").Click();
        cut.WaitForAssertion(() => Resolver(cut).TextContent.Should().Contain(CopyText), Timeout);
        ResolverButton(cut, "Quedarme con la copia").Click();

        ResolverButton(cut, "Cerrar sin guardar").Click();

        cut.WaitForAssertion(() => (Shell.EditingPath, Read(Original).Contains(CopyText)).Should().Be((null, true)), Timeout);
    }

    [Fact]
    public void Decide_later_closes_the_resolver_and_keeps_the_notice()
    {
        var cut = RenderResolver();

        ResolverButton(cut, "Decidir más tarde").Click();

        cut.WaitForAssertion(() =>
        {
            Shell.ConflictsOpen.Should().BeFalse();
            cut.Find("header.ds-frame__strip").TextContent.Should().Contain("conflicto");
        }, Timeout);
    }

    [Fact]
    public async Task A_map_state_conflict_shows_a_summary_of_each_version_instead_of_the_raw_json()
    {
        const string State = "Mapas/cripta.state.json";
        const string StateCopy = "Mapas/cripta.sync-conflict-20261103-101500-ABCDEF1.state.json";
        Directory.CreateDirectory(Path.GetDirectoryName(_vault.PathOf(State))!);
        File.WriteAllText(_vault.PathOf(State), "{\"version\":1,\"tokens\":[{\"id\":\"a\",\"name\":\"Goblin\",\"side\":\"enemy\",\"size\":1,\"x\":1,\"y\":1}],\"strokes\":[]}");
        File.WriteAllText(_vault.PathOf(StateCopy), "{\"version\":1,\"tokens\":[],\"strokes\":[{\"id\":\"s\",\"color\":\"#fff\",\"width\":2,\"points\":[0,0,1,1,2,2]},{\"id\":\"s2\",\"color\":\"#fff\",\"width\":2,\"points\":[0,0,1,1,2,2]}]}");
        await Services.GetRequiredService<VaultHost>().OpenAsync(_vault.Root);
        var cut = Render<DmShell>();
        cut.WaitForAssertion(() => cut.FindAll("button").First(b => b.TextContent.Trim() == "Revisar"), Timeout);
        await cut.InvokeAsync(() => Shell.OpenConflicts());
        // El vault de ejemplo ya trae el conflicto de una nota, que va delante: se elige el del mapa en el desplegable.
        cut.WaitForAssertion(() => Resolver(cut).TextContent.Should().Contain(CopyText), Timeout);
        var select = Resolver(cut).QuerySelector("select")!;
        select.Change(select.QuerySelectorAll("option").First(o => o.TextContent == State).GetAttribute("value"));

        cut.WaitForAssertion(() =>
        {
            var texts = Resolver(cut).QuerySelectorAll("section p").Select(p => p.TextContent).ToList();
            texts.Should().HaveCount(2);
            texts[0].Should().StartWith("1 token, 0 trazos, modificado el ");
            texts[1].Should().StartWith("0 tokens, 2 trazos, modificado el ");
            Resolver(cut).QuerySelectorAll("pre").Should().BeEmpty();
        }, Timeout);
    }

    [Fact]
    public async Task A_map_state_conflict_summary_mentions_combat_round()
    {
        const string State = "Mapas/cripta.state.json";
        const string StateCopy = "Mapas/cripta.sync-conflict-20261103-101500-ABCDEF1.state.json";
        Directory.CreateDirectory(Path.GetDirectoryName(_vault.PathOf(State))!);
        File.WriteAllText(_vault.PathOf(State), "{\"version\":2,\"tokens\":[{\"id\":\"a\",\"name\":\"Goblin\",\"side\":\"enemy\",\"size\":1,\"x\":1,\"y\":1}],\"strokes\":[],\"combat\":{\"round\":2,\"currentEntryId\":\"e1\",\"entries\":[{\"id\":\"e1\",\"tokenId\":\"a\",\"initiative\":10,\"phase\":null}]}}");
        File.WriteAllText(_vault.PathOf(StateCopy), "{\"version\":2,\"tokens\":[],\"strokes\":[]}");
        await Services.GetRequiredService<VaultHost>().OpenAsync(_vault.Root);
        var cut = Render<DmShell>();
        cut.WaitForAssertion(() => cut.FindAll("button").First(b => b.TextContent.Trim() == "Revisar"), Timeout);
        await cut.InvokeAsync(() => Shell.OpenConflicts());
        cut.WaitForAssertion(() => Resolver(cut).TextContent.Should().Contain(CopyText), Timeout);
        var select = Resolver(cut).QuerySelector("select")!;
        select.Change(select.QuerySelectorAll("option").First(o => o.TextContent == State).GetAttribute("value"));

        cut.WaitForAssertion(() =>
        {
            var texts = Resolver(cut).QuerySelectorAll("section p").Select(p => p.TextContent).ToList();
            texts.Should().HaveCount(2);
            texts[0].Should().StartWith("1 token, 0 trazos, combate en ronda 2, modificado el ");
            texts[1].Should().StartWith("0 tokens, 0 trazos, modificado el ");
        }, Timeout);
    }
}
