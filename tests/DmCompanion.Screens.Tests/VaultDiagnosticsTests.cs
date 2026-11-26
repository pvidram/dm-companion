using Bunit;
using DmCompanion.Core.Glossary;
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

public sealed class VaultDiagnosticsTests : BunitContext, IAsyncLifetime
{
    private readonly TempVault _vault = TempVault.Empty();
    private readonly InMemoryAppPreferences _preferences = new();

    public VaultDiagnosticsTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddDmCompanionUI();
        Services.AddSingleton<IAppPreferences>(_preferences);
        Services.AddSingleton<IVaultServiceFactory>(new FileSystemVaultServiceFactory(NullLoggerFactory.Instance));
        Services.AddSingleton<VaultHost>();
        Services.AddSingleton<PortraitUrlProvider>();
        Services.AddSingleton<VaultFileUrls>();
        Services.AddSingleton<JournalIndex>();
        Services.AddScoped<ShellState>();
        Services.AddGlossaryForTests();
        Services.AddScoped<WikilinkInterop>();
        Services.AddManualsForTests();
    }

    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

    async Task IAsyncLifetime.DisposeAsync()
    {
        await Services.GetRequiredService<VaultHost>().DisposeAsync();
        await base.DisposeAsync();
        _vault.Dispose();
    }

    private async Task<IRenderedComponent<VaultDiagnostics>> RenderAsync(params (string Path, string System)[] campaigns)
    {
        foreach (var (path, system) in campaigns)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_vault.PathOf(path))!);
            await File.WriteAllTextAsync(_vault.PathOf(path), $"---\ntype: campaign\nname: C\nsystem: {system}\n---\n");
        }
        await Services.GetRequiredService<VaultHost>().OpenAsync(_vault.Root);
        // Con el listado ya hecho, el único aviso que queda es el del relistado de OnInitializedAsync: no hay listado perezoso que lo confunda.
        var journal = Services.GetRequiredService<JournalIndex>();
        await journal.RefreshImagesAsync();
        var relisted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        journal.Changed += (_, _) => relisted.TrySetResult();
        var cut = Render<VaultDiagnostics>();
        // OnInitializedAsync relista las imágenes y repinta al acabar: hasta entonces los manejadores de los botones aún cambian. Esperar
        // a «dos renders» no sirve: si el listado acaba antes del primer await solo hay uno.
        await relisted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await cut.InvokeAsync(() => { });
        return cut;
    }

    [Fact]
    public async Task Lists_a_discarded_campaign()
    {
        var cut = await RenderAsync(("Campana.md", "cosmere"), ("Otra/Vieja.md", "dnd5e"));
        cut.Markup.Should().Contain("Otra/Vieja.md");
    }

    [Fact]
    public async Task Shows_the_active_system()
    {
        var cut = await RenderAsync(("Campana.md", "cosmere"));
        cut.Markup.Should().Contain("Cosmere RPG");
    }

    [Fact]
    public async Task Without_campaign_note_says_so()
    {
        var cut = await RenderAsync();
        cut.Markup.Should().Contain("sin nota de campaña");
    }

    [Fact]
    public async Task Lists_sheet_warnings_with_the_note_path()
    {
        Directory.CreateDirectory(_vault.PathOf("Personajes"));
        await File.WriteAllTextAsync(_vault.PathOf("Personajes/Lyra.md"),
            "---\ntype: pc\nname: Lyra\nstats:\n  abilities: { dex: 14 }\n  skills:\n    stealth: pro\n---\n");
        var cut = await RenderAsync();
        cut.Markup.Should().Contain("Personajes/Lyra.md: stats.skills.stealth: valor desconocido");
    }

    [Fact]
    public async Task With_a_log_folder_it_shows_the_open_button()
    {
        Services.AddSingleton<ILogFolder>(new FakeLogFolder());
        var cut = await RenderAsync();

        cut.FindAll("button").Should().Contain(b => b.TextContent.Contains("Abrir carpeta de registros"));
    }

    [Fact]
    public async Task Without_update_notice_it_hides_the_update_toggle()
    {
        var cut = await RenderAsync();

        cut.Markup.Should().NotContain("Buscar versiones nuevas al arrancar");
    }

    [Fact]
    public async Task With_update_notice_the_update_toggle_is_checked()
    {
        Services.AddSingleton(new UpdateNotice(_preferences, new FakeUpdateCheck()));
        var cut = await RenderAsync();

        cut.Markup.Should().Contain("Buscar versiones nuevas al arrancar");
        cut.Find("input[type=checkbox]").HasAttribute("checked").Should().BeTrue();
    }

    [Fact]
    public async Task Unchecking_the_update_toggle_stores_false()
    {
        Services.AddSingleton(new UpdateNotice(_preferences, new FakeUpdateCheck()));
        var cut = await RenderAsync();

        await cut.InvokeAsync(() => cut.Find("input[type=checkbox]").Change(false));

        _preferences.Get(UpdateNotice.CheckKey).Should().Be("false");
    }

    [Fact]
    public async Task The_log_folder_button_calls_open()
    {
        var logs = new FakeLogFolder();
        Services.AddSingleton<ILogFolder>(logs);
        var cut = await RenderAsync();

        await cut.InvokeAsync(() => cut.FindAll("button").First(b => b.TextContent.Contains("Abrir carpeta de registros")).Click());

        logs.Opens.Should().Be(1);
    }

    [Fact]
    public async Task When_the_log_folder_cannot_be_opened_it_shows_a_notice()
    {
        Services.AddSingleton<ILogFolder>(new FakeLogFolder { Succeeds = false });
        var cut = await RenderAsync();

        await cut.InvokeAsync(() => cut.FindAll("button").First(b => b.TextContent.Contains("Abrir carpeta de registros")).Click());

        cut.Markup.Should().Contain("No se pudo abrir la carpeta de registros");
    }

    [Fact]
    public async Task When_the_log_folder_opens_there_is_no_notice()
    {
        Services.AddSingleton<ILogFolder>(new FakeLogFolder());
        var cut = await RenderAsync();

        await cut.InvokeAsync(() => cut.FindAll("button").First(b => b.TextContent.Contains("Abrir carpeta de registros")).Click());

        cut.Markup.Should().NotContain("No se pudo abrir la carpeta de registros");
    }

    [Fact]
    public async Task Without_a_log_folder_it_shows_no_button()
    {
        var cut = await RenderAsync();

        cut.Markup.Should().NotContain("Abrir carpeta de registros");
    }

    [Fact]
    public async Task With_an_unavailable_log_folder_it_shows_no_button()
    {
        Services.AddSingleton<ILogFolder>(new FakeLogFolder { IsAvailable = false });
        var cut = await RenderAsync();

        cut.Markup.Should().NotContain("Abrir carpeta de registros");
    }

    [Fact]
    public async Task It_shows_the_version()
    {
        var cut = await RenderAsync();

        cut.Markup.Should().Contain("Versión:");
    }

    [Fact]
    public async Task About_section_shows_the_attribution()
    {
        var cut = await RenderAsync();

        cut.Markup.Should().Contain("Acerca de").And.Contain("Creative Commons").And.Contain("https://www.dndbeyond.com/srd");
    }

    [Fact]
    public async Task Glossary_warnings_are_listed()
    {
        Services.AddSingleton<IGlossaryPackSource>(new NoSpellsSource());
        await Services.GetRequiredService<VaultHost>().OpenAsync(_vault.Root);
        // Diagnóstico solo informa de los paquetes ya intentados: se provoca la carga de conjuros antes.
        Services.GetRequiredService<GlossaryService>().GetSrd("srd:conjuros/fireball");
        var cut = await RenderAsync();

        cut.Markup.Should().Contain("<h3>Glosario</h3>").And.Contain("conjuros");
    }

    [Fact]
    public async Task Without_glossary_warnings_there_is_no_Glosario_section()
    {
        var cut = await RenderAsync();

        cut.Markup.Should().NotContain("<h3>Glosario</h3>");
    }

    [Fact]
    public async Task Corrupt_bookmarks_are_listed()
    {
        Directory.CreateDirectory(_vault.PathOf("manuales/dnd5e"));
        await File.WriteAllTextAsync(_vault.PathOf("manuales/dnd5e/PHB.marcadores.json"), "{ roto");
        var cut = await RenderAsync();
        // Después de abrir el vault: reabrirlo vacía la lista de marcadores dañados.
        await Services.GetRequiredService<ManualBookmarkStore>().LoadAsync("manuales/dnd5e/PHB.pdf");
        cut.Render();

        cut.Markup.Should().Contain("<h3>Marcadores de manuales</h3>").And.Contain("manuales/dnd5e/PHB.marcadores.json");
    }

    [Fact]
    public async Task Lists_the_sync_conflicts_of_a_journal_entry()
    {
        Directory.CreateDirectory(_vault.PathOf("Personajes/Lyra/Diario"));
        const string entry = "---\ntype: journal\nadded: 2026-11-16T21:34:00+01:00\nimages:\n  - \"a.png\"\n---\n";
        await File.WriteAllTextAsync(_vault.PathOf("Personajes/Lyra/Diario/2026-11-16 Ankheg.md"), entry);
        await File.WriteAllTextAsync(_vault.PathOf("Personajes/Lyra/Diario/2026-11-16 Ankheg.sync-conflict-20261116-101500-ABCDEF1.md"), entry);
        await File.WriteAllBytesAsync(_vault.PathOf("Personajes/Lyra/Diario/a.png"), [1, 2, 3]);

        var cut = await RenderAsync();

        // La imagen existe: la única fila con la nota es la del conflicto, no un aviso del diario.
        cut.WaitForAssertion(() => cut.FindAll("li").Select(li => li.TextContent).Should().Contain("Personajes/Lyra/Diario/2026-11-16 Ankheg.md"), TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Lists_the_warnings_of_journal_entries_with_their_path()
    {
        Directory.CreateDirectory(_vault.PathOf("Personajes/Lyra/Diario"));
        await File.WriteAllTextAsync(_vault.PathOf("Personajes/Lyra/Diario/2026-11-16 Rota.md"), "---\ntype: journal\nimages: [sin cerrar\n---\n");
        await File.WriteAllTextAsync(_vault.PathOf("Personajes/Lyra/Diario/2026-11-17 Falta.md"),
            "---\ntype: journal\nadded: 2026-11-17T10:00:00+01:00\nimages:\n  - \"falta.png\"\n---\n");

        var cut = await RenderAsync();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Avisos del diario");
            cut.Markup.Should().Contain("Personajes/Lyra/Diario/2026-11-16 Rota.md: Frontmatter YAML inválido");
            cut.Markup.Should().Contain("Personajes/Lyra/Diario/2026-11-17 Falta.md: Imagen no encontrada: falta.png");
        }, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Without_journal_warnings_the_diary_section_is_not_shown()
    {
        var cut = await RenderAsync();

        cut.Markup.Should().NotContain("Avisos del diario");
    }

    private sealed class NoSpellsSource : IGlossaryPackSource
    {
        public Stream? Open(string packName) => packName == "conjuros" ? null : new EmbeddedGlossaryPackSource().Open(packName);
    }

    private sealed class FakeLogFolder : ILogFolder
    {
        public bool IsAvailable { get; init; } = true;
        public int Opens { get; private set; }
        public bool Succeeds { get; init; } = true;

        public bool Open()
        {
            Opens++;
            return Succeeds;
        }
    }
}
