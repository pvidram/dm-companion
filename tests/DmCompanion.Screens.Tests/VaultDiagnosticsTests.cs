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

public sealed class VaultDiagnosticsTests : BunitContext, IAsyncLifetime
{
    private readonly TempVault _vault = TempVault.Empty();

    public VaultDiagnosticsTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddDmCompanionUI();
        Services.AddSingleton<IAppPreferences>(new InMemoryAppPreferences());
        Services.AddSingleton<IVaultServiceFactory>(new FileSystemVaultServiceFactory(NullLoggerFactory.Instance));
        Services.AddSingleton<VaultHost>();
        Services.AddSingleton<PortraitUrlProvider>();
        Services.AddScoped<ShellState>();
        Services.AddScoped<WikilinkInterop>();
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
        return Render<VaultDiagnostics>();
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
}
