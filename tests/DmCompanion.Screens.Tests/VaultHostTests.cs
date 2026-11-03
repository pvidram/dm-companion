using DmCompanion.Core.Vault;
using DmCompanion.Core.Vault.Storage;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Screens.Tests;

public sealed class VaultHostTests : IAsyncLifetime
{
    private readonly TempVault _first = TempVault.FromDemo();
    private readonly TempVault _second = TempVault.FromDemo();
    private readonly InMemoryAppPreferences _preferences = new();
    private readonly VaultHost _host;

    public VaultHostTests() =>
        _host = new VaultHost(new FileSystemVaultServiceFactory(NullLoggerFactory.Instance), _preferences, NullLogger<VaultHost>.Instance);

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _host.DisposeAsync();
        _first.Dispose();
        _second.Dispose();
    }

    [Fact]
    public async Task OpenAsync_on_demo_copy_returns_true()
    {
        var opened = await _host.OpenAsync(_first.Root);

        opened.Should().BeTrue();
    }

    [Fact]
    public async Task OpenAsync_on_demo_copy_loads_its_notes()
    {
        await _host.OpenAsync(_first.Root);

        _host.Current!.Index.Count.Should().Be(8);
    }

    [Fact]
    public async Task OpenAsync_on_demo_copy_saves_the_path()
    {
        await _host.OpenAsync(_first.Root);

        _preferences.Get("vault_path").Should().Be(_first.Root);
    }

    [Fact]
    public async Task OpenAsync_on_missing_folder_returns_false()
    {
        var opened = await _host.OpenAsync(Path.Combine(_first.Root, "no-existe"));

        opened.Should().BeFalse();
    }

    [Fact]
    public async Task OpenAsync_on_missing_folder_leaves_no_vault()
    {
        await _host.OpenAsync(Path.Combine(_first.Root, "no-existe"));

        _host.Current.Should().BeNull();
    }

    [Fact]
    public async Task OpenAsync_on_missing_folder_reports_the_error()
    {
        await _host.OpenAsync(Path.Combine(_first.Root, "no-existe"));

        _host.LastError.Should().NotBeNull();
    }

    [Fact]
    public async Task Concurrent_OpenAsync_calls_end_with_one_of_the_two_vaults()
    {
        var results = await Task.WhenAll(_host.OpenAsync(_first.Root), _host.OpenAsync(_second.Root));

        results.Should().OnlyContain(opened => opened);
        _host.Current!.RootPath.Should().BeOneOf(_first.Root, _second.Root);
    }

    [Fact]
    public async Task ForgetAsync_closes_the_vault()
    {
        await _host.OpenAsync(_first.Root);

        await _host.ForgetAsync();

        _host.Current.Should().BeNull();
    }

    [Fact]
    public async Task ForgetAsync_removes_the_saved_path()
    {
        await _host.OpenAsync(_first.Root);

        await _host.ForgetAsync();

        _preferences.Get("vault_path").Should().BeNull();
    }
}

public sealed class VaultHostSwapTests : IAsyncLifetime
{
    private readonly TempVault _first = TempVault.FromDemo();
    private readonly TempVault _second = TempVault.FromDemo();
    private readonly InMemoryAppPreferences _preferences = new();
    private readonly GatedFactory _factory = new(new FileSystemVaultServiceFactory(NullLoggerFactory.Instance));
    private readonly VaultHost _host;

    public VaultHostSwapTests() => _host = new VaultHost(_factory, _preferences, NullLogger<VaultHost>.Instance);

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        _factory.Release();
        await _host.DisposeAsync();
        _first.Dispose();
        _second.Dispose();
    }

    [Fact]
    public async Task OpenAsync_failure_keeps_the_previous_vault_open()
    {
        await _host.OpenAsync(_first.Root);
        var previous = _host.Current;

        var opened = await _host.OpenAsync(Path.Combine(_second.Root, "no-existe"));

        opened.Should().BeFalse();
        _host.Current.Should().BeSameAs(previous);
        previous!.Index.Count.Should().Be(8);
    }

    [Fact]
    public async Task OpenAsync_failure_reports_the_error_and_keeps_the_saved_path()
    {
        await _host.OpenAsync(_first.Root);

        await _host.OpenAsync(Path.Combine(_second.Root, "no-existe"));

        _host.LastError.Should().NotBeNull();
        _preferences.Get("vault_path").Should().Be(_first.Root);
    }

    [Fact]
    public async Task OpenAsync_success_swaps_to_the_new_vault()
    {
        await _host.OpenAsync(_first.Root);

        var opened = await _host.OpenAsync(_second.Root);

        opened.Should().BeTrue();
        _host.Current!.RootPath.Should().Be(_second.Root);
        _host.LastError.Should().BeNull();
    }

    [Fact]
    public async Task ForgetAsync_waits_for_an_open_in_progress()
    {
        _factory.Block();
        var open = Task.Run(() => _host.OpenAsync(_first.Root));
        _factory.WaitUntilCreateEntered();

        var forget = _host.ForgetAsync();
        await Task.Delay(150);
        forget.IsCompleted.Should().BeFalse();

        _factory.Release();
        await open;
        await forget;
        _host.Current.Should().BeNull();
    }

    [Fact]
    public async Task DisposeAsync_waits_for_an_open_in_progress()
    {
        _factory.Block();
        var open = Task.Run(() => _host.OpenAsync(_first.Root));
        _factory.WaitUntilCreateEntered();

        var dispose = _host.DisposeAsync().AsTask();
        await Task.Delay(150);
        dispose.IsCompleted.Should().BeFalse();

        _factory.Release();
        await open;
        await dispose;
        _host.Current.Should().BeNull();
    }

    [Fact]
    public async Task OpenAsync_assigns_the_new_vault_even_if_disposing_the_previous_one_throws()
    {
        var host = new VaultHost(new ThrowingOnDisposeFactory(), _preferences, NullLogger<VaultHost>.Instance);
        await host.OpenAsync(_first.Root);

        var opened = await host.OpenAsync(_second.Root);

        opened.Should().BeTrue();
        host.Current!.RootPath.Should().Be(_second.Root);
        host.LastError.Should().BeNull();
        await host.Invoking(h => h.DisposeAsync().AsTask()).Should().NotThrowAsync();
    }

    private sealed class ThrowingOnDisposeFactory : IVaultServiceFactory
    {
        public VaultService Create(string rootPath) => new(
            new FileSystemVaultStorage(rootPath), new ThrowingChangeSource(), NullLogger<VaultService>.Instance);
    }

    private sealed class ThrowingChangeSource : IVaultChangeSource
    {
        public event EventHandler<VaultChange>? Changed { add { } remove { } }
        public void Start() { }
        public void Dispose() => throw new InvalidOperationException("dispose roto");
    }

    private sealed class GatedFactory(IVaultServiceFactory inner) : IVaultServiceFactory
    {
        private readonly ManualResetEventSlim _entered = new(false);
        private readonly ManualResetEventSlim _go = new(true);

        public void Block() => _go.Reset();
        public void Release() => _go.Set();
        public void WaitUntilCreateEntered() => _entered.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();

        public VaultService Create(string rootPath)
        {
            _entered.Set();
            _go.Wait(TimeSpan.FromSeconds(10));
            return inner.Create(rootPath);
        }
    }
}
