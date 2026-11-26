using DmCompanion.Core.Glossary;
using DmCompanion.Core.Vault.Storage;
using DmCompanion.Desktop;
using DmCompanion.Desktop.Components;
using DmCompanion.Desktop.Logging;
using DmCompanion.Desktop.Platform;
using DmCompanion.DevHost;
using DmCompanion.DevHost.Platform;
using DmCompanion.Core.Tv;
using DmCompanion.Infrastructure;
using DmCompanion.Infrastructure.Tv;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Platform;
using DmCompanion.Screens.Services;
using DmCompanion.UI;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using System.Runtime.InteropServices;

// ContentRoot en la carpeta del ejecutable: así encuentra wwwroot y los manifiestos de activos aunque se lance desde otra carpeta.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = AppContext.BaseDirectory });

// Desktop:SingleInstance=false (tests): sin mutex, sin título de consola y sin abrir el navegador.
var desktopMode = builder.Configuration.GetValue("Desktop:SingleInstance", true);
// En modo escritorio un fallo de arranque se muestra en la consola y espera a Intro: si no, la ventana se cierra sin rastro.
var waitOnError = StartupFailure.ShouldWait(builder.Configuration);

DailyFileLoggerProvider? fileLogs = null;
SingleInstance? instance = null;
string? instanceFile = null;
var hasStarted = false;
try
{
    if (desktopMode && !InstallationCheck.IsComplete(AppContext.BaseDirectory))
        return StartupFailure.Report(
            "Faltan ficheros de DM Companion junto al ejecutable. Descomprime el zip antes de abrir DMCompanion.exe.",
            null, null, waitOnError, Console.Out, Console.In);

    var paths = new DesktopPaths(builder.Configuration);
    instanceFile = paths.InstanceFile;

    // Registro en fichero solo en modo escritorio; los tests de integración no escriben en el perfil real.
    // Se crea antes que el host para poder registrar también el aviso de «ya está abierto».
    fileLogs = desktopMode ? new DailyFileLoggerProvider(paths.LogsFolder, TimeProvider.System) : null;
    if (fileLogs is not null) builder.Logging.AddProvider(fileLogs);

    instance = desktopMode ? SingleInstance.TryAcquire(builder.Configuration["Desktop:MutexName"] is { Length: > 0 } mutexName ? mutexName : @"Local\DmCompanion.Desktop", paths.InstanceFile) : null;
    if (desktopMode && instance is null)
    {
        // Un doble clic rápido llega antes de que la primera instancia escriba instance.json: se espera un poco.
        if (await SingleInstance.WaitForUrlAsync(paths.InstanceFile, TimeProvider.System, TimeSpan.FromSeconds(10)) is { } runningUrl)
        {
            new BrowserLauncher(Microsoft.Extensions.Logging.Abstractions.NullLogger<BrowserLauncher>.Instance).Open(runningUrl);
            return 0;
        }
        return StartupFailure.Report(
            "DM Companion ya está abierto, pero no se encuentra su dirección. Cierra su ventana de consola y vuelve a abrirlo.",
            fileLogs?.CreateLogger("DmCompanion.Desktop"), null, waitOnError, Console.Out, Console.In);
    }

    if (desktopMode && OperatingSystem.IsWindows())
        Console.Title = "DM Companion — no cierres esta ventana mientras juegas";

    // Solo loopback, siempre: el host sirve el vault sin autenticación. Un Listen explícito anula
    // ASPNETCORE_URLS y --urls, así que no se puede ampliar desde fuera. En tests manda TestServer.
    // Incondicional: con TestServer es inocuo y así --urls nunca quita el pin a loopback.
    // Desktop:Port cambia el puerto preferido (0 = uno libre elegido por el sistema); por defecto, el habitual 5190.
    var preferredPort = builder.Configuration.GetValue("Desktop:Port", 5190);
    builder.WebHost.ConfigureKestrel(options => options.ListenLocalhost(PortSelector.Choose(preferredPort)));

    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    builder.Services.AddSingleton(paths);
    builder.Services.AddDmCompanionUI();
    builder.Services.AddSingleton<IVaultServiceFactory, FileSystemVaultServiceFactory>();
    builder.Services.AddSingleton<IFolderBrowser, DirectoryBrowser>();
    builder.Services.AddSingleton<IAppPreferences>(sp =>
        new JsonFileAppPreferences(paths.PreferencesFile, sp.GetRequiredService<ILogger<JsonFileAppPreferences>>()));
    builder.Services.AddSingleton<IStorageAccess, DesktopStorageAccess>();
    builder.Services.AddSingleton<ISampleVault, DesktopSampleVault>();
    builder.Services.AddSingleton<IBrowserLauncher, BrowserLauncher>();
    builder.Services.AddSingleton<ILogFolder, DesktopLogFolder>();
    builder.Services.AddSingleton<VaultHost>();
    builder.Services.AddSingleton<MapSession>();
    builder.Services.AddSingleton<TokenHitPoints>();
    builder.Services.AddSingleton<CombatController>();
    builder.Services.AddSingleton<MapLibrary>();
    builder.Services.AddSingleton<IGlossaryPackSource, EmbeddedGlossaryPackSource>();
    builder.Services.AddSingleton<GlossaryPackLoader>();
    builder.Services.AddSingleton<GlossaryService>();
    builder.Services.AddSingleton<AudioLibraryService>();
    builder.Services.AddSingleton<ManualLibrary>();
    builder.Services.AddSingleton<ManualBookmarkStore>();
    builder.Services.AddSingleton<ManualPositions>();
    builder.Services.AddScoped<ManualViewerInterop>();
    builder.Services.AddScoped<AudioMixer>();
    builder.Services.AddSingleton<AudioStore>();
    builder.Services.AddScoped<ShellState>();
    builder.Services.AddScoped<WikilinkInterop>();
    builder.Services.AddScoped<MapCanvasInterop>();
    builder.Services.AddScoped<PlayerPreview>();
    builder.Services.AddScoped<PlayerPreviewInterop>();
    builder.Services.AddScoped<TurnStripInterop>();
    builder.Services.AddScoped<DragPanelInterop>();
    builder.Services.AddScoped<IAudioPlayer, WebAudioPlayer>();
    builder.Services.AddScoped<BackNavigation>();
    builder.Services.AddSingleton<VaultFileUrls>();
    builder.Services.AddSingleton<NoteImageResolver>();
    builder.Services.AddSingleton<PortraitUrlProvider>();
    builder.Services.AddSingleton<JournalStore>();
    builder.Services.AddSingleton<ITvAssets, EmbeddedTvAssets>();
    builder.Services.AddSingleton<ITvServer, TvServer>();
    builder.Services.AddSingleton<TvMapFeed>();
    builder.Services.AddSingleton<TvContentFeed>();
    builder.Services.AddSingleton<TvSession>();
    builder.Services.AddSingleton<JournalIndex>();
    builder.Services.AddSingleton<JournalLogService>();
    builder.Services.AddHostedService<MapFlushOnStop>();

    var app = builder.Build();

    app.Lifetime.ApplicationStarted.Register(() =>
    {
        var server = app.Services.GetRequiredService<IServer>();
        // Solo Kestrel abre sockets: un servidor en memoria (TestServer) no tiene direcciones que vigilar.
        if (!KestrelDetection.IsKestrel(server)) return;

        // Una feature ausente o vacía cuenta como no loopback: se detiene.
        var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses ?? [];
        if (!LoopbackGuard.IsLoopbackOnly(addresses))
        {
            app.Logger.LogCritical("DM Companion escucha fuera de loopback ({Addresses}). Se detiene: no tiene autenticación.", string.Join(", ", addresses));
            app.Lifetime.StopApplication();
            return;
        }

        if (!desktopMode) return;

        var url = addresses.First();
        SingleInstance.WriteUrl(paths.InstanceFile, url);
        Console.WriteLine($"DM Companion está en marcha en {url}");
        Console.WriteLine($"Registros en {paths.LogsFolder}");
        app.Services.GetRequiredService<IBrowserLauncher>().Open(url);
    });

    // Al salir se borra instance.json para que una segunda instancia no lea una URL obsoleta.
    if (desktopMode) app.Lifetime.ApplicationStopping.Register(() => SingleInstance.DeleteUrl(paths.InstanceFile));

    // Cerrar la consola con la X (CTRL_CLOSE_EVENT) mata el proceso en cuanto vuelve el manejador. El ConsoleLifetime de
    // .NET 10 ya trae HandleWindowsShutdown y probablemente basta para parar el host y guardar el mapa; este manejador es
    // una red de seguridad (en Windows SIGTERM cubre CTRL_CLOSE, LOGOFF y SHUTDOWN) y guarda de forma síncrona. Los dos
    // guardados no se pisan porque MapSession serializa el acceso a disco con _ioGate. Pendiente de confirmar en Windows
    // cerrando con la X. La registración vive hasta el final del proceso.
    var mapSession = desktopMode ? app.Services.GetRequiredService<MapSession>() : null;
    using var shutdownSignal = mapSession is null
        ? null
        : PosixSignalRegistration.Create(PosixSignal.SIGTERM, _ => ShutdownFlush.FlushBlocking(mapSession, TimeSpan.FromSeconds(4), app.Logger));

    app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

    app.UseAntiforgery();

    app.MapStaticAssets();
    app.MapVaultFiles();
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode()
        .AddAdditionalAssemblies(typeof(VaultDiagnostics).Assembly);

    app.Lifetime.ApplicationStarted.Register(() => hasStarted = true);
    app.Run();
}
catch (Exception ex) when (desktopMode && ex is not HostAbortedException)
{
    return StartupFailure.Report(
        StartupFailure.UnexpectedFailureMessage(hasStarted),
        fileLogs?.CreateLogger("DmCompanion.Desktop"), ex, waitOnError, Console.Out, Console.In);
}
finally
{
    // Solo la instancia que posee el mutex borra instance.json: la segunda no debe quitar la dirección de la viva.
    if (instanceFile is not null) SingleInstance.DeleteUrlIfOwner(instance, instanceFile);
    instance?.Dispose();
    fileLogs?.Dispose();
}
return 0;

public partial class Program;
