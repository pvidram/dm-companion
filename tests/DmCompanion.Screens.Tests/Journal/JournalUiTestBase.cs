using Bunit;
using DmCompanion.Core.Vault.Storage;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Platform;
using DmCompanion.Screens.Services;
using DmCompanion.Screens.Tests.Support;
using DmCompanion.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Screens.Tests;

/// <summary>
/// bUnit sobre una copia del vault de demo (Lyra y Torvald) con el diario registrado. Las clases derivadas escriben sus
/// entradas en <see cref="SeedAsync"/>, que corre antes de abrir el vault, y añaden los servicios que necesiten en su constructor.
/// </summary>
public abstract class JournalUiTestBase : BunitContext, IAsyncLifetime
{
    protected const string Lyra = "Personajes/Lyra.md";
    protected const string Torvald = "Personajes/Torvald.md";
    protected const string LyraDiary = "Personajes/Lyra/Diario";
    protected const string TorvaldDiary = "Personajes/Torvald/Diario";
    protected static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    protected JournalUiTestBase()
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
        Services.AddWindowingForTests();
        Services.AddScoped<WikilinkInterop>();
        Services.AddGlossaryForTests();
        Services.AddSingleton(TimeProvider.System);
        Services.AddSingleton<JournalIndex>();
    }

    private protected TempVault Vault { get; } = TempVault.FromDemo();
    protected VaultHost Host => Services.GetRequiredService<VaultHost>();
    protected ShellState Shell => Services.GetRequiredService<ShellState>();
    protected JournalIndex Journal => Services.GetRequiredService<JournalIndex>();

    async Task IAsyncLifetime.InitializeAsync()
    {
        await SeedAsync();
        await Host.OpenAsync(Vault.Root);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await Host.DisposeAsync();
        await base.DisposeAsync();
        Vault.Dispose();
    }

    protected virtual Task SeedAsync() => Task.CompletedTask;

    protected void WriteFile(string relativePath, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Vault.PathOf(relativePath))!);
        File.WriteAllText(Vault.PathOf(relativePath), content);
    }

    protected void WriteImage(string relativePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Vault.PathOf(relativePath))!);
        File.WriteAllBytes(Vault.PathOf(relativePath), [1, 2, 3]);
    }

    /// <summary>Texto de una entrada. <paramref name="images"/> son nombres relativos a la carpeta del diario.</summary>
    protected static string EntryText(string added, string[] images, string? creature = null, string note = "", string type = "journal") =>
        $"---\ntype: {type}\nadded: {added}\nimages:\n{string.Concat(images.Select(name => $"  - \"{name}\"\n"))}"
        + (creature is null ? string.Empty : $"creature: \"{creature}\"\n") + $"---\n{note}\n";

    protected static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Wait;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException();
            await Task.Delay(20);
        }
    }
}
