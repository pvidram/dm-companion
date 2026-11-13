using DmCompanion.DevHost.Services;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.DevHost.Tests;

public sealed class DevVaultSeederTests : IAsyncLifetime
{
    private readonly TempVault _demo = TempVault.FromDemo();
    private readonly TempVault _work = TempVault.Empty();
    private readonly InMemoryAppPreferences _preferences = new();
    private VaultHost? _host;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_host is not null) await _host.DisposeAsync();
        _demo.Dispose();
        _work.Dispose();
    }

    private (DevVaultSeeder Seeder, VaultHost Host) Create(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        _host = new VaultHost(new FileSystemVaultServiceFactory(NullLoggerFactory.Instance), _preferences, NullLogger<VaultHost>.Instance);
        return (new DevVaultSeeder(configuration, new FakeEnvironment(_work.Root), _host, NullLogger<DevVaultSeeder>.Instance), _host);
    }

    [Fact]
    public async Task Seeds_the_default_vault_when_it_is_missing()
    {
        var target = Path.Combine(_work.Root, "vault-demo");
        var (seeder, host) = Create(new() { ["DevHost:DefaultVaultPath"] = target, ["DevHost:SeedFrom"] = _demo.Root });

        await seeder.StartAsync();

        host.Current!.Index.Count.Should().Be(11);
    }

    [Fact]
    public async Task Leaves_no_seeding_folder_behind()
    {
        var target = Path.Combine(_work.Root, "vault-demo");
        var (seeder, _) = Create(new() { ["DevHost:DefaultVaultPath"] = target, ["DevHost:SeedFrom"] = _demo.Root });

        await seeder.StartAsync();

        Directory.Exists(target + ".seeding").Should().BeFalse();
    }

    [Fact]
    public async Task Does_not_seed_over_an_explicit_vault_path()
    {
        var explicitPath = Path.Combine(_work.Root, "mi-vault");
        Directory.CreateDirectory(explicitPath);
        var (seeder, host) = Create(new() { ["DevHost:VaultPath"] = explicitPath, ["DevHost:SeedFrom"] = _demo.Root });

        await seeder.StartAsync();

        host.Current!.Index.Count.Should().Be(0);
        Directory.EnumerateFileSystemEntries(explicitPath).Should().BeEmpty();
    }

    [Fact]
    public async Task Does_not_create_a_missing_explicit_vault_path()
    {
        var explicitPath = Path.Combine(_work.Root, "no-existe");
        var (seeder, host) = Create(new() { ["DevHost:VaultPath"] = explicitPath, ["DevHost:SeedFrom"] = _demo.Root });

        await seeder.StartAsync();

        host.Current.Should().BeNull();
        Directory.Exists(explicitPath).Should().BeFalse();
    }

    private sealed class FakeEnvironment(string contentRoot) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = contentRoot;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
