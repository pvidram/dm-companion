using DmCompanion.Core.Glossary;
using DmCompanion.Core.Vault.Storage;
using DmCompanion.Desktop;
using DmCompanion.Desktop.Components;
using DmCompanion.Desktop.Logging;
using DmCompanion.Desktop.Platform;
using DmCompanion.Desktop.Updates;
using DmCompanion.Desktop.Window;
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

// Desktop:SingleInstance=false (tests): sin mutex, sin ventana y sin endpoints de escritorio.
var desktopMode = builder.Configuration.GetValue("Desktop:SingleInstance", true);

DailyFileLoggerProvider? fileLogs = null;
SingleInstance? instance = null;
string? instanceFile = null;
var hasStarted = false;
var windowClosedNormally = false;
var logsFolder = "la carpeta de registros";
// La URL de Kestrel, o null si el guardián de loopback detuvo el host antes de que la ventana se abriera.
var startedUrl = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
try
{
    if (desktopMode && !InstallationCheck.IsComplete(AppContext.BaseDirectory))
        return StartupFailure.Report(
            "Faltan ficheros de DM Companion junto al ejecutable. Reinstala DM Companion o vuelve a descomprimir el zip.",
            null, null, NativeDialog.ShowError);

    var paths = new DesktopPaths(builder.Configuration);
    instanceFile = paths.InstanceFile;
    logsFolder = paths.LogsFolder;

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
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            if (await FocusRequest.SendAsync(http, runningUrl, TimeProvider.System, CancellationToken.None)) return 0;
            return StartupFailure.Report("DM Companion ya está abierto, pero no responde. Ciérralo desde el Administrador de tareas y vuelve a abrirlo.",
                fileLogs?.CreateLogger("DmCompanion.Desktop"), null, NativeDialog.ShowError);
        }
        return StartupFailure.Report(
            "DM Companion ya está abierto, pero no se encuentra su dirección. Ciérralo y vuelve a abrirlo.",
            fileLogs?.CreateLogger("DmCompanion.Desktop"), null, NativeDialog.ShowError);
    }

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
    builder.Services.AddSingleton<IVaultCreator, FileSystemVaultCreator>();
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
    builder.Services.AddScoped<PanelLayoutStore>();
    builder.Services.AddScoped<WindowManager>();
    builder.Services.AddScoped<WikilinkInterop>();
    builder.Services.AddScoped<MapCanvasInterop>();
    builder.Services.AddScoped<PlayerPreview>();
    builder.Services.AddScoped<PlayerPreviewInterop>();
    builder.Services.AddScoped<TurnStripInterop>();
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
    if (desktopMode)
    {
        // El cliente tipado es transitorio, pero UpdateNotice es singleton y solo consulta una vez por arranque: la instancia única es aceptable.
        builder.Services.AddHttpClient<IUpdateCheck, GitHubUpdateCheck>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(5);
                client.MaxResponseContentBufferSize = 1024 * 1024;
            })
            .RemoveAllLoggers();
        builder.Services.AddSingleton(sp => new UpdateNotice(sp.GetRequiredService<IAppPreferences>(), sp.GetRequiredService<IUpdateCheck>()));
        builder.Services.AddHostedService<UpdateCheckOnStart>();
        builder.Services.AddSingleton(sp => new WindowPlacementStore(paths.WindowFile, sp.GetRequiredService<ILogger<WindowPlacementStore>>()));
        builder.Services.AddSingleton<DesktopWindow>();
        builder.Services.AddSingleton<IWindowActivator>(sp => sp.GetRequiredService<DesktopWindow>());
    }

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
            startedUrl.TrySetResult(null);
            app.Lifetime.StopApplication();
            return;
        }

        if (!desktopMode) return;

        var url = addresses.First();
        SingleInstance.WriteUrl(paths.InstanceFile, url);
        startedUrl.TrySetResult(url);
    });

    // Al salir se borra instance.json para que una segunda instancia no lea una URL obsoleta.
    if (desktopMode) app.Lifetime.ApplicationStopping.Register(() => SingleInstance.DeleteUrl(paths.InstanceFile));

    // Cubren el apagado pedido desde fuera en Linux (SIGTERM, SIGINT): la ventana se cierra sin el aviso de fallo y,
    // con SIGTERM, el mapa se guarda de forma síncrona (MapSession serializa el acceso a disco con _ioGate, así que no
    // se pisa con el guardado del host). En Windows la app es WinExe y no recibe CTRL_LOGOFF ni CTRL_SHUTDOWN: ahí
    // el mapa depende de su guardado diferido (MapSession.SaveDelay). La registración vive hasta el final del proceso.
    var mapSession = desktopMode ? app.Services.GetRequiredService<MapSession>() : null;
    var desktopWindow = desktopMode ? app.Services.GetRequiredService<DesktopWindow>() : null;
    using var shutdownSignal = mapSession is null
        ? null
        : PosixSignalRegistration.Create(PosixSignal.SIGTERM, _ =>
        {
            desktopWindow?.CloseSilentlyOnStop();
            ShutdownFlush.FlushBlocking(mapSession, TimeSpan.FromSeconds(4), app.Logger);
        });
    // Ctrl+C también es un apagado pedido por el usuario: la ventana se cierra sin el aviso de fallo.
    using var interruptSignal = desktopWindow is null
        ? null
        : PosixSignalRegistration.Create(PosixSignal.SIGINT, _ => desktopWindow.CloseSilentlyOnStop());

    app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

    app.UseAntiforgery();

    app.MapStaticAssets();
    app.MapVaultFiles();
    if (desktopMode) app.MapDesktopEndpoints();
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode()
        .AddAdditionalAssemblies(typeof(VaultDiagnostics).Assembly);

    app.Lifetime.ApplicationStarted.Register(() => hasStarted = true);
    if (!desktopMode)
    {
        app.Run();
        return 0;
    }

    await app.StartAsync();
    if (await startedUrl.Task is not { } windowUrl)
    {
        await app.StopAsync();
        return StartupFailure.Report("DM Companion se ha detenido: escuchaba fuera del propio equipo.",
            fileLogs?.CreateLogger("DmCompanion.Desktop"), null, NativeDialog.ShowError);
    }

    // Desktop:NoWindow (tests con Kestrel real): el host sirve sin abrir ninguna ventana hasta que se para.
    if (builder.Configuration.GetValue("Desktop:NoWindow", false))
    {
        await app.WaitForShutdownAsync();
        return 0;
    }

    if (OperatingSystem.IsWindows() && !WebView2Runtime.IsInstalled())
    {
        await app.StopAsync();
        return StartupFailure.Report(
            "Falta Microsoft Edge WebView2. Descárgalo de https://developer.microsoft.com/microsoft-edge/webview2/ y vuelve a abrir DM Companion.",
            fileLogs?.CreateLogger("DmCompanion.Desktop"), null, NativeDialog.ShowError);
    }

    string? windowFailure = null;
    try
    {
        await RunWindowAsync(desktopWindow!, windowUrl, app.Lifetime.ApplicationStopping);
        windowClosedNormally = true;
    }
    catch (Exception ex) when (StartupFailure.MissingNativeLibraryMessage(ex, OperatingSystem.IsLinux()) is { } missing)
    {
        windowFailure = missing;
        fileLogs?.CreateLogger("DmCompanion.Desktop").LogError("No se pudo cargar la librería nativa ({ExceptionType}).", ex.GetType().FullName);
    }
    finally
    {
        // Siempre se para el host (guarda el mapa), también si la ventana falla; el error llega al informe general.
        using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            await app.StopAsync(stopTimeout.Token);
        }
        catch (OperationCanceledException ex) when (stopTimeout.IsCancellationRequested)
        {
            app.Logger.LogWarning("El host tardó más de 10 s en parar ({ExceptionType}).", ex.GetType().FullName);
        }
    }
    if (windowFailure is not null) return StartupFailure.Report(windowFailure, null, null, NativeDialog.ShowError);
}
catch (Exception ex) when (desktopMode && ex is not HostAbortedException)
{
    return StartupFailure.Report(
        StartupFailure.UnexpectedFailureMessage(hasStarted, logsFolder),
        fileLogs?.CreateLogger("DmCompanion.Desktop"), ex, NativeDialog.ShowError);
}
finally
{
    // Solo la instancia que posee el mutex borra instance.json: la segunda no debe quitar la dirección de la viva.
    if (instanceFile is not null) SingleInstance.DeleteUrlIfOwner(instance, instanceFile);
    instance?.Dispose();
    fileLogs?.Dispose();
}
// Mutex, instance.json and logs are already closed here; see NativeExit for why the process skips the native exit path.
if (NativeExit.ShouldSkipNativeTeardown(OperatingSystem.IsLinux(), windowClosedNormally)) NativeExit.Now(0);
return 0;

// Photino necesita un hilo STA en Windows, y el Main asíncrono de top-level statements no lo es. Por eso la ventana
// vive en un hilo propio que se espera sin bloquear. macOS (que exige el hilo principal) no está soportado.
static Task RunWindowAsync(DesktopWindow window, string url, CancellationToken hostStopping)
{
    var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var thread = new Thread(() =>
    {
        try
        {
            window.Run(url, hostStopping);
            closed.SetResult();
        }
        catch (Exception ex)
        {
            closed.SetException(ex);
        }
    }) { Name = "DesktopWindow", IsBackground = false };
    if (OperatingSystem.IsWindows()) thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    return closed.Task;
}

public partial class Program;
