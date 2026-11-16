using DmCompanion.Core.Glossary;
using DmCompanion.Core.Vault.Storage;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Platform;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Screens.Tests.Services;

public sealed class GlossaryServiceTests : IAsyncLifetime
{
    private readonly TempVault _vault = TempVault.FromDemo();
    private readonly VaultHost _host = new(
        new FileSystemVaultServiceFactory(NullLoggerFactory.Instance), new InMemoryAppPreferences(), NullLogger<VaultHost>.Instance);

    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _host.DisposeAsync();
        _vault.Dispose();
    }

    private GlossaryService Create(IGlossaryPackSource? source = null) => new(
        _host, new GlossaryPackLoader(source ?? new EmbeddedGlossaryPackSource(), NullLogger<GlossaryPackLoader>.Instance),
        NullLogger<GlossaryService>.Instance);

    [Fact]
    public async Task Dnd5e_vault_lists_srd_entries()
    {
        await _host.OpenAsync(_vault.Root);
        using var glossary = Create();

        glossary.Index.Search(new GlossaryQuery(Text: "bola de fuego")).Should().Contain(row => row.Key.StartsWith("srd:conjuros/"));
    }

    [Fact]
    public async Task Cosmere_vault_lists_only_vault_notes()
    {
        await File.WriteAllTextAsync(_vault.PathOf("Campaña.md"), "---\ntype: campaign\nname: C\nsystem: cosmere\n---\n");
        Directory.CreateDirectory(_vault.PathOf("Glosario"));
        await File.WriteAllTextAsync(_vault.PathOf("Glosario/Investidura.md"), "---\ntype: rule\nname: Investidura\n---\nTexto.\n");
        await _host.OpenAsync(_vault.Root);
        using var glossary = Create();

        glossary.Index.Search(new GlossaryQuery()).Should().OnlyContain(row => row.IsVault);
        glossary.Index.Search(new GlossaryQuery()).Should().ContainSingle(row => row.Name == "Investidura");
    }

    [Fact]
    public async Task Vault_change_rebuilds_the_index_and_raises_Changed()
    {
        await _host.OpenAsync(_vault.Root);
        using var glossary = Create();
        var before = glossary.Index;
        var raised = new TaskCompletionSource();
        glossary.Changed += (_, _) => raised.TrySetResult();

        Directory.CreateDirectory(_vault.PathOf("Glosario"));
        await _host.Current!.SaveAsync("Glosario/Aboleth.md", "---\ntype: monster\nname: Aboleth\n---\nMi aboleth.\n", WritePrecondition.MustNotExist);
        await raised.Task.WaitAsync(TimeSpan.FromSeconds(10));

        glossary.Index.Search(new GlossaryQuery(Text: "aboleth")).Should().Contain(row => row.IsVault && row.Name == "Aboleth");
        glossary.Index.Should().NotBeSameAs(before);
    }

    [Fact]
    public async Task Index_is_not_rebuilt_while_nothing_changes()
    {
        await _host.OpenAsync(_vault.Root);
        using var glossary = Create();

        glossary.Index.Should().BeSameAs(glossary.Index);
    }

    [Fact]
    public async Task Missing_pack_shows_up_as_a_warning()
    {
        await _host.OpenAsync(_vault.Root);
        using var glossary = Create(new MissingPackSource("conjuros"));
        glossary.GetSrd("srd:conjuros/fireball");

        glossary.Warnings.Should().ContainSingle().Which.Should().Contain("conjuros");
    }

    [Fact]
    public async Task Warnings_does_not_load_entry_packs()
    {
        await _host.OpenAsync(_vault.Root);
        var source = new OpenLogSource();
        using var glossary = Create(source);

        var warnings = glossary.Warnings;

        (warnings.Count, string.Join(",", source.Opened)).Should().Be((0, "indice"));
    }

    [Fact]
    public async Task GetSrd_returns_null_for_packs_the_system_does_not_use()
    {
        await _host.OpenAsync(_vault.Root);
        using var dnd = Create();
        var key = dnd.Index.Search(new GlossaryQuery(Text: "bola de fuego")).First().Key;
        dnd.GetSrd(key).Should().NotBeNull();

        await File.WriteAllTextAsync(_vault.PathOf("Campaña.md"), "---\ntype: campaign\nname: C\nsystem: cosmere\n---\n");
        await _host.OpenAsync(_vault.Root);

        dnd.GetSrd(key).Should().BeNull();
    }

    private sealed class OpenLogSource : IGlossaryPackSource
    {
        public List<string> Opened { get; } = [];

        public Stream? Open(string packName)
        {
            Opened.Add(packName);
            return new EmbeddedGlossaryPackSource().Open(packName);
        }
    }

    private sealed class MissingPackSource(string missing) : IGlossaryPackSource
    {
        public Stream? Open(string packName) => packName == missing ? null : new EmbeddedGlossaryPackSource().Open(packName);
    }
}
