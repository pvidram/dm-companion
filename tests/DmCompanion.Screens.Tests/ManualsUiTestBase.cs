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

/// <summary>Contexto común de las pruebas de la pestaña Manuales y del visor: vault de demo (con el PDF de muestra) y el módulo JS del visor simulado.</summary>
public abstract class ManualsUiTestBase : BunitContext, IAsyncLifetime
{
    protected const string PdfPath = "manuales/dnd5e/Manual de prueba.pdf";

    private readonly TempVault _vault = TempVault.FromDemo();
    protected readonly BunitJSModuleInterop Module;
    protected readonly IAppPreferences Prefs = new InMemoryAppPreferences();

    protected ManualsUiTestBase()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Module = JSInterop.SetupModule(ManualViewerInterop.ModulePath);
        // En modo Loose un bool sin configurar sería false, que el visor entiende como «no se pudo pintar».
        Module.Setup<bool>("showPage", _ => !PageFailsToPaint && !HoldShowPage).SetResult(true);
        Module.Setup<bool>("showPage", _ => PageFailsToPaint && !HoldShowPage).SetResult(false);
        var held = Module.Setup<bool>("showPage", _ => HoldShowPage);
        HeldShowPage = result => held.SetResult(result);
        Services.AddDmCompanionUI();
        Services.AddSingleton<IAppPreferences>(Prefs);
        Services.AddSingleton<IVaultServiceFactory>(new FileSystemVaultServiceFactory(NullLoggerFactory.Instance));
        Services.AddSingleton<VaultHost>();
        Services.AddSingleton<VaultFileUrls>();
        Services.AddScoped<ShellState>();
        Services.AddWindowingForTests();
        Services.AddScoped<ManualViewerInterop>();
        Services.AddGlossaryForTests();
        Services.AddManualsForTests();
    }

    protected string VaultRoot => _vault.Root;

    /// <summary>true hace que showPage devuelva false (página que no se pudo pintar).</summary>
    protected bool PageFailsToPaint { get; set; }

    /// <summary>true deja showPage pendiente hasta que se complete <see cref="HeldShowPage"/>.</summary>
    protected bool HoldShowPage { get; set; }

    protected Action<bool> HeldShowPage { get; private set; } = null!;

    protected ShellState Shell => Services.GetRequiredService<ShellState>();

    protected static string PageKey => $"manual.page.{PdfPath}";

    protected void SetupOpen(ManualDocInfo? info) => Module.Setup<ManualDocInfo?>("open", _ => true).SetResult(info);

    async Task IAsyncLifetime.InitializeAsync()
    {
        await Services.GetRequiredService<VaultHost>().OpenAsync(_vault.Root);
        await Services.GetRequiredService<ManualLibrary>().RefreshAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await Services.GetRequiredService<VaultHost>().DisposeAsync();
        await base.DisposeAsync();
        _vault.Dispose();
    }
}
