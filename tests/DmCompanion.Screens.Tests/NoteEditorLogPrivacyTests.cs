using Bunit;
using DmCompanion.Core.Vault;
using DmCompanion.Core.Vault.Storage;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Platform;
using DmCompanion.Screens.Services;
using DmCompanion.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Screens.Tests;

// Regla de privacidad: un fallo al guardar registra ruta y tipo de excepción, nunca el mensaje (que podría citar la nota).
public sealed class NoteEditorLogPrivacyTests : BunitContext, IAsyncLifetime
{
    private const string Lyra = "Personajes/Lyra.md";
    private const string SecretMarker = "SECRETO-9f3a71c2";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly TempVault _vault = TempVault.FromDemo();
    private readonly TextLogger _logger = new();

    public NoteEditorLogPrivacyTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddDmCompanionUI();
        Services.AddSingleton<IAppPreferences>(new InMemoryAppPreferences());
        Services.AddSingleton<IVaultServiceFactory>(new ExplodingWriteFactory());
        Services.AddSingleton<VaultHost>();
        Services.AddSingleton<VaultFileUrls>();
        Services.AddSingleton<PortraitUrlProvider>();
        Services.AddScoped<ShellState>();
        Services.AddSingleton(TimeProvider.System);
        Services.AddSingleton<MapSession>();
        Services.AddSingleton<TokenHitPoints>();
        Services.AddSingleton<CombatController>();
        Services.AddSingleton<MapLibrary>();
        Services.AddScoped<MapCanvasInterop>();
        Services.AddScoped<TurnStripInterop>();
        Services.AddScoped<DragPanelInterop>();
        Services.AddScoped<WikilinkInterop>();
        Services.AddSingleton<ILogger<NoteEditor>>(_logger);
    }

    Task IAsyncLifetime.InitializeAsync() => Services.GetRequiredService<VaultHost>().OpenAsync(_vault.Root);

    async Task IAsyncLifetime.DisposeAsync()
    {
        await Services.GetRequiredService<VaultHost>().DisposeAsync();
        await base.DisposeAsync();
        _vault.Dispose();
    }

    private string FailedSaveLog()
    {
        var shell = Services.GetRequiredService<ShellState>();
        var cut = Render<DmShell>();
        cut.InvokeAsync(() => shell.EditNote(Lyra)).GetAwaiter().GetResult();
        cut.WaitForAssertion(() => cut.Find("textarea"), Timeout);
        cut.InvokeAsync(() => shell.RaiseEditorCommand(EditorCommand.Overwrite)).GetAwaiter().GetResult();
        cut.WaitForAssertion(() => _logger.Text.Should().Contain(Lyra), Timeout);
        return _logger.Text;
    }

    [Fact]
    public void Failed_save_logs_the_path_and_the_exception_type() =>
        FailedSaveLog().Should().Contain(Lyra).And.Contain(nameof(InvalidOperationException));

    [Fact]
    public void Failed_save_log_does_not_contain_text_quoted_by_the_exception() =>
        FailedSaveLog().Should().NotContain(SecretMarker);

    private sealed class ExplodingWriteFactory : IVaultServiceFactory
    {
        public VaultService Create(string rootPath) =>
            new(new ExplodingWriteStorage(new FileSystemVaultStorage(rootPath)), new NoChanges(), NullLogger<VaultService>.Instance);
    }

    private sealed class NoChanges : IVaultChangeSource
    {
        public event EventHandler<VaultChange>? Changed { add { } remove { } }
        public void Start() { }
        public void Dispose() { }
    }

    private sealed class ExplodingWriteStorage(IVaultStorage inner) : IVaultStorage
    {
        public string RootPath => inner.RootPath;
        public Task<IReadOnlyList<VaultFile>> ListMarkdownFilesAsync(CancellationToken ct = default) => inner.ListMarkdownFilesAsync(ct);
        public Task<VaultFileContent> ReadAsync(string relativePath, CancellationToken ct = default) => inner.ReadAsync(relativePath, ct);
        public Task<byte[]> ReadBytesAsync(string relativePath, CancellationToken ct = default) => inner.ReadBytesAsync(relativePath, ct);
        public Task<VaultFileInfo?> StatAsync(string relativePath, CancellationToken ct = default) => inner.StatAsync(relativePath, ct);
        public Task<WriteResult> WriteAsync(string relativePath, string content, WritePrecondition precondition, CancellationToken ct = default) =>
            throw new InvalidOperationException($"error cerca de «{SecretMarker}»");
        public Task<IReadOnlyList<VaultFile>> ListFilesAsync(Func<string, bool> predicate, CancellationToken ct = default) => inner.ListFilesAsync(predicate, ct);
        public Task<Stream> OpenReadAsync(string relativePath, CancellationToken ct = default) => inner.OpenReadAsync(relativePath, ct);
        public Task<WriteResult> WriteBytesAsync(string relativePath, Stream content, WritePrecondition precondition, CancellationToken ct = default) =>
            inner.WriteBytesAsync(relativePath, content, precondition, ct);
        public Task<bool> MoveAsync(string fromRelativePath, string toRelativePath, CancellationToken ct = default) => inner.MoveAsync(fromRelativePath, toRelativePath, ct);
        public Task<IReadOnlyList<SyncConflict>> FindConflictsAsync(CancellationToken ct = default) => inner.FindConflictsAsync(ct);
        public Task DeleteAsync(string relativePath, CancellationToken ct = default) => inner.DeleteAsync(relativePath, ct);
    }

    private sealed class TextLogger : ILogger<NoteEditor>
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<string> _lines = new();
        public string Text => string.Join('\n', _lines);

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            _lines.Enqueue($"{formatter(state, exception)}\n{exception}");
    }
}
