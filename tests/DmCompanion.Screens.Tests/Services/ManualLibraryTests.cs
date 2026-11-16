using DmCompanion.Infrastructure;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Screens.Tests.Services;

public sealed class ManualLibraryTests : IAsyncDisposable
{
    private readonly TempVault _vault = TempVault.Empty();
    private readonly VaultHost _host = new(new FileSystemVaultServiceFactory(NullLoggerFactory.Instance), new InMemoryAppPreferences(), NullLogger<VaultHost>.Instance);

    public async ValueTask DisposeAsync()
    {
        await _host.DisposeAsync();
        _vault.Dispose();
    }

    private void Put(string relative)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_vault.PathOf(relative))!);
        File.WriteAllBytes(_vault.PathOf(relative), [1, 2, 3]);
    }

    private void SetCampaign(string system) =>
        File.WriteAllText(_vault.PathOf("Campana.md"), $"---\ntype: campaign\nname: C\nsystem: {system}\n---\n");

    private async Task<ManualLibrary> OpenAsync()
    {
        await _host.OpenAsync(_vault.Root);
        var library = new ManualLibrary(_host, NullLogger<ManualLibrary>.Instance);
        await library.RefreshAsync();
        return library;
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline) await Task.Delay(50);
        condition().Should().BeTrue();
    }

    [Fact]
    public async Task Lists_only_active_system_manuals()
    {
        Put("manuales/dnd5e/PHB.pdf");
        Put("manuales/cosmere/Otro.pdf");
        using var library = await OpenAsync();

        library.Manuals.Select(m => m.Path).Should().Equal("manuales/dnd5e/PHB.pdf");
    }

    [Fact]
    public async Task Cosmere_vault_lists_cosmere_folder()
    {
        SetCampaign("cosmere");
        Put("manuales/dnd5e/PHB.pdf");
        Put("manuales/cosmere/Otro.pdf");
        using var library = await OpenAsync();

        library.Manuals.Select(m => m.Path).Should().Equal("manuales/cosmere/Otro.pdf");
        library.SystemId.Should().Be("cosmere");
    }

    [Fact]
    public async Task Groups_by_subfolder()
    {
        Put("manuales/dnd5e/Zeta.pdf");
        Put("manuales/dnd5e/Reglas/Beta.pdf");
        Put("manuales/dnd5e/Reglas/Alfa.pdf");
        using var library = await OpenAsync();

        library.Manuals.Should().Equal(
            new ManualInfo("manuales/dnd5e/Zeta.pdf", "Zeta", ""),
            new ManualInfo("manuales/dnd5e/Reglas/Alfa.pdf", "Alfa", "Reglas"),
            new ManualInfo("manuales/dnd5e/Reglas/Beta.pdf", "Beta", "Reglas"));
    }

    [Fact]
    public async Task Pdf_added_raises_Changed()
    {
        Directory.CreateDirectory(_vault.PathOf("manuales/dnd5e"));
        using var library = await OpenAsync();
        var raised = 0;
        library.Changed += (_, _) => Interlocked.Increment(ref raised);

        Put("manuales/dnd5e/Nuevo.pdf");

        await WaitForAsync(() => library.Manuals.Count == 1 && raised > 0);
    }

    [Fact]
    public async Task No_folder_gives_empty_list()
    {
        using var library = await OpenAsync();

        library.Manuals.Should().BeEmpty();
    }
}
