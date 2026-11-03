using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Infrastructure.Tests;

public sealed class FileSystemVaultServiceFactoryTests : IDisposable
{
    private readonly TempVault _vault = TempVault.FromDemo();
    private readonly FileSystemVaultServiceFactory _factory = new(NullLoggerFactory.Instance);

    public void Dispose() => _vault.Dispose();

    [Fact]
    public async Task Create_on_demo_vault_loads_its_notes()
    {
        await using var service = _factory.Create(_vault.Root);
        await service.LoadAsync();

        service.Index.Count.Should().Be(8);
    }

    [Fact]
    public void Create_on_missing_folder_throws_DirectoryNotFoundException()
    {
        var act = () => _factory.Create(Path.Combine(_vault.Root, "no-existe"));

        act.Should().Throw<DirectoryNotFoundException>();
    }
}
