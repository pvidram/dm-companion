using DmCompanion.Screens.Tests.Support;
using Bunit;
using DmCompanion.Core.Vault.Storage;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Pages;
using DmCompanion.Screens.Platform;
using DmCompanion.Screens.Services;
using DmCompanion.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Screens.Tests;

public sealed class HomeTests : BunitContext, IAsyncLifetime
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly TempVault _vault = TempVault.FromDemo();
    private readonly InMemoryAppPreferences _preferences = new();
    private readonly FakeStorageAccess _storage;

    public HomeTests()
    {
        _storage = new FakeStorageAccess { DefaultBrowseRoot = _vault.Root };
        JSInterop.Mode = JSRuntimeMode.Loose; // la librería de UI importa focus-trap.js
        Services.AddDmCompanionUI();
        Services.AddSingleton<IAppPreferences>(_preferences);
        Services.AddSingleton<IStorageAccess>(_storage);
        Services.AddSingleton<IFolderBrowser, DirectoryBrowser>();
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
    }

    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

    async Task IAsyncLifetime.DisposeAsync()
    {
        await Services.GetRequiredService<VaultHost>().DisposeAsync();
        await base.DisposeAsync();
        _vault.Dispose();
    }

    [Fact]
    public void Without_access_it_shows_the_storage_access_screen()
    {
        _storage.HasAllFilesAccess = false;

        var cut = Render<Home>();

        cut.Markup.Should().Contain("Conceder acceso");
        cut.Markup.Should().NotContain("Usar esta carpeta");
    }

    [Fact]
    public void Grant_button_requests_access()
    {
        _storage.HasAllFilesAccess = false;
        var cut = Render<Home>();

        ButtonWith(cut, "Conceder acceso").Click();

        cut.WaitForAssertion(() => _storage.Requests.Should().Be(1), TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void Already_granted_without_access_shows_a_notice_and_stays()
    {
        _storage.HasAllFilesAccess = false;
        var cut = Render<Home>();

        ButtonWith(cut, "Ya lo he concedido").Click();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("ds-notice"), TimeSpan.FromSeconds(10));
        cut.Markup.Should().Contain("Conceder acceso");
    }

    [Fact]
    public void Already_granted_with_access_moves_to_the_picker()
    {
        _storage.HasAllFilesAccess = false;
        var cut = Render<Home>();
        _storage.HasAllFilesAccess = true;

        ButtonWith(cut, "Ya lo he concedido").Click();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Usar esta carpeta"));
    }

    [Fact]
    public void Without_a_vault_it_shows_the_picker_listing_subfolders()
    {
        var cut = Render<Home>();

        cut.Markup.Should().Contain("Usar esta carpeta");
        cut.Markup.Should().Contain(_vault.Root);
        foreach (var name in Directory.GetDirectories(_vault.Root).Select(Path.GetFileName).Where(n => !n!.StartsWith('.')))
            cut.Markup.Should().Contain(name!);
    }

    [Fact]
    public void Picker_with_no_subfolders_shows_an_empty_state()
    {
        using var empty = TempVault.Empty();
        _storage.DefaultBrowseRoot = empty.Root;

        var cut = Render<Home>();

        cut.Markup.Should().Contain("ds-empty");
    }

    [Fact]
    public void Up_is_disabled_at_the_browse_root()
    {
        var cut = Render<Home>();

        ButtonWith(cut, "Atrás").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void Entering_a_subfolder_enables_up_and_returns_to_the_root()
    {
        var cut = Render<Home>();
        var sub = Directory.GetDirectories(_vault.Root).Select(Path.GetFileName).First(n => !n!.StartsWith('.'))!;

        cut.FindAll(".ds-list-row").First(r => r.TextContent.Contains(sub)).Click();
        cut.WaitForAssertion(() => ButtonWith(cut, "Atrás").HasAttribute("disabled").Should().BeFalse(), TimeSpan.FromSeconds(10));
        cut.Markup.Should().Contain(Path.Combine(_vault.Root, sub));

        ButtonWith(cut, "Atrás").Click();
        cut.WaitForAssertion(() => ButtonWith(cut, "Atrás").HasAttribute("disabled").Should().BeTrue(), TimeSpan.FromSeconds(10));
        cut.Markup.Should().Contain("Usar esta carpeta");
    }

    [Fact]
    public async Task Use_this_folder_opens_the_vault()
    {
        var cut = Render<Home>();

        ButtonWith(cut, "Usar esta carpeta").Click();

        var host = Services.GetRequiredService<VaultHost>();
        await cut.WaitForAssertionAsync(() => host.Current.Should().NotBeNull(), Timeout);
        host.Current!.Index.Count.Should().Be(11);
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Cambiar vault"), Timeout);
    }

    [Fact]
    public void A_failed_open_shows_the_error_in_the_picker()
    {
        _storage.DefaultBrowseRoot = Path.Combine(_vault.Root, "no-existe");
        var cut = Render<Home>();

        ButtonWith(cut, "Usar esta carpeta").Click();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("No se puede abrir el vault"), Timeout);
    }

    [Fact]
    public void On_start_it_opens_the_saved_path()
    {
        _preferences.Set("vault_path", _vault.Root);

        var cut = Render<Home>();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Cambiar vault"), Timeout);
    }

    [Fact]
    public async Task While_opening_it_shows_a_loading_state()
    {
        _preferences.Set("vault_path", _vault.Root);
        var factory = new BlockingFactory();
        Services.AddSingleton<IVaultServiceFactory>(factory);
        var host = Services.GetRequiredService<VaultHost>();
        // Una apertura en curso retiene el semáforo del host: la de Home espera y muestra «Abriendo».
        var pending = Task.Run(() => host.OpenAsync(_vault.Root));
        factory.Entered.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();

        var cut = Render<Home>();

        cut.Markup.Should().Contain("Abriendo");
        factory.Go.Set();
        await pending;
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Cambiar vault"), Timeout);
    }

    [Fact]
    public async Task A_pending_open_of_the_saved_path_does_not_load_the_vault_twice()
    {
        _preferences.Set("vault_path", _vault.Root);
        var factory = new BlockingFactory();
        Services.AddSingleton<IVaultServiceFactory>(factory);
        var host = Services.GetRequiredService<VaultHost>();
        var pending = Task.Run(() => host.OpenAsync(_vault.Root));
        factory.Entered.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();

        var cut = Render<Home>();
        factory.Go.Set();
        await pending;
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Cambiar vault"), Timeout);

        factory.Creates.Should().Be(1);
    }

    [Fact]
    public void Without_sample_vault_it_shows_the_picker()
    {
        var cut = Render<Home>();

        cut.Markup.Should().NotContain("Bienvenido a DM Companion");
    }

    [Fact]
    public void With_a_sample_vault_and_no_saved_path_it_shows_the_welcome()
    {
        Services.AddSingleton<ISampleVault>(new FakeSampleVault(_vault.Root));

        var cut = Render<Home>();

        cut.Markup.Should().Contain("Bienvenido a DM Companion");
    }

    [Fact]
    public void With_a_sample_vault_it_does_not_show_the_picker_yet()
    {
        Services.AddSingleton<ISampleVault>(new FakeSampleVault(_vault.Root));

        var cut = Render<Home>();

        cut.Markup.Should().NotContain("Usar esta carpeta");
    }

    [Fact]
    public void With_a_saved_path_it_skips_the_welcome()
    {
        _preferences.Set("vault_path", _vault.Root);
        Services.AddSingleton<ISampleVault>(new FakeSampleVault(_vault.Root));

        var cut = Render<Home>();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Cambiar vault"), Timeout);
    }

    [Fact]
    public void Open_own_vault_moves_to_the_picker()
    {
        Services.AddSingleton<ISampleVault>(new FakeSampleVault(_vault.Root));
        var cut = Render<Home>();

        cut.FindAll("button").First(b => b.TextContent.Contains("Abrir mi vault")).Click();

        cut.Markup.Should().Contain("Usar esta carpeta");
    }

    [Fact]
    public void Try_sample_prepares_it_and_opens_the_returned_path()
    {
        var sample = new FakeSampleVault(_vault.Root);
        Services.AddSingleton<ISampleVault>(sample);
        var cut = Render<Home>();

        cut.FindAll("button").First(b => b.TextContent.Contains("Probar con el vault de ejemplo")).Click();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Cambiar vault"), Timeout);
        sample.Prepares.Should().Be(1);
    }

    [Fact]
    public async Task Try_sample_opens_the_vault_at_the_returned_path()
    {
        Services.AddSingleton<ISampleVault>(new FakeSampleVault(_vault.Root));
        var cut = Render<Home>();

        cut.FindAll("button").First(b => b.TextContent.Contains("Probar con el vault de ejemplo")).Click();

        var host = Services.GetRequiredService<VaultHost>();
        await cut.WaitForAssertionAsync(() => host.Current!.RootPath.Should().Be(_vault.Root), Timeout);
    }

    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    public void Try_sample_failure_shows_the_notice(Type exceptionType)
    {
        Services.AddSingleton<ISampleVault>(new FakeSampleVault(_vault.Root) { Failure = (Exception)Activator.CreateInstance(exceptionType)! });
        var cut = Render<Home>();

        cut.FindAll("button").First(b => b.TextContent.Contains("Probar con el vault de ejemplo")).Click();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("No se pudo preparar el vault de ejemplo."), Timeout);
    }

    [Fact]
    public void Try_sample_failure_logs_only_the_exception_type()
    {
        var logger = new CapturingLogger();
        Services.AddSingleton<ILogger<Home>>(logger);
        Services.AddSingleton<ISampleVault>(new FakeSampleVault(_vault.Root) { Failure = new IOException("C:\\secreto\\ruta") });
        var cut = Render<Home>();

        cut.FindAll("button").First(b => b.TextContent.Contains("Probar con el vault de ejemplo")).Click();

        cut.WaitForAssertion(() => logger.Text.Should().Contain("IOException").And.NotContain("secreto"), Timeout);
    }

    [Fact]
    public void Try_sample_failure_opens_nothing()
    {
        Services.AddSingleton<ISampleVault>(new FakeSampleVault(_vault.Root) { Failure = new IOException() });
        var cut = Render<Home>();

        cut.FindAll("button").First(b => b.TextContent.Contains("Probar con el vault de ejemplo")).Click();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("No se pudo preparar"), Timeout);
        Services.GetRequiredService<VaultHost>().Current.Should().BeNull();
    }

    [Fact]
    public void Try_sample_with_an_invalid_path_shows_the_open_error()
    {
        Services.AddSingleton<ISampleVault>(new FakeSampleVault(Path.Combine(_vault.Root, "no-existe")));
        var cut = Render<Home>();

        cut.FindAll("button").First(b => b.TextContent.Contains("Probar con el vault de ejemplo")).Click();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("No se puede abrir el vault"), Timeout);
    }

    [Fact]
    public void Try_sample_shows_the_loading_state_while_preparing()
    {
        var sample = new GatedSampleVault();
        Services.AddSingleton<ISampleVault>(sample);
        var cut = Render<Home>();

        cut.FindAll("button").First(b => b.TextContent.Contains("Probar con el vault de ejemplo")).Click();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Abriendo el vault"), Timeout);
        sample.Gate.SetResult(_vault.Root);
    }

    [Fact]
    public void Try_sample_hides_the_welcome_buttons_while_preparing()
    {
        var sample = new GatedSampleVault();
        Services.AddSingleton<ISampleVault>(sample);
        var cut = Render<Home>();

        cut.FindAll("button").First(b => b.TextContent.Contains("Probar con el vault de ejemplo")).Click();

        cut.WaitForAssertion(() => cut.Markup.Should().NotContain("Probar con el vault de ejemplo"), Timeout);
        sample.Gate.SetResult(_vault.Root);
    }

    private sealed class GatedSampleVault : ISampleVault
    {
        public bool IsAvailable => true;
        public TaskCompletionSource<string> Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<string> PrepareAsync() => Gate.Task;
    }

    private sealed class CapturingLogger : ILogger<Home>
    {
        private readonly List<string> _lines = [];
        public string Text => string.Join('\n', _lines);
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            _lines.Add(formatter(state, exception) + (exception is null ? "" : " " + exception));
    }

    private sealed class FakeSampleVault(string path) : ISampleVault
    {
        public bool IsAvailable => true;
        public int Prepares { get; private set; }
        public Exception? Failure { get; init; }

        public Task<string> PrepareAsync()
        {
            Prepares++;
            return Failure is null ? Task.FromResult(path) : Task.FromException<string>(Failure);
        }
    }

    private static AngleSharp.Dom.IElement ButtonWith(IRenderedComponent<Home> cut, string text) =>
        cut.FindAll("button").First(b => b.TextContent.Contains(text));

    private sealed class BlockingFactory : IVaultServiceFactory
    {
        public ManualResetEventSlim Entered { get; } = new(false);
        public ManualResetEventSlim Go { get; } = new(false);
        private int _creates;
        public int Creates => _creates;

        public DmCompanion.Core.Vault.VaultService Create(string rootPath)
        {
            Interlocked.Increment(ref _creates);
            Entered.Set();
            Go.Wait(TimeSpan.FromSeconds(10));
            return new FileSystemVaultServiceFactory(NullLoggerFactory.Instance).Create(rootPath);
        }
    }

    private sealed class FakeStorageAccess : IStorageAccess
    {
        public bool HasAllFilesAccess { get; set; } = true;
        public string DefaultBrowseRoot { get; set; } = "/";
        public int Requests { get; private set; }
        public void RequestAllFilesAccess() => Requests++;
    }
}
