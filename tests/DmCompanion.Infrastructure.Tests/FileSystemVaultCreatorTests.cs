using DmCompanion.Core.Vault.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Infrastructure.Tests;

public sealed class FileSystemVaultCreatorTests : IDisposable
{
    private readonly TempVault _vault = TempVault.Empty();
    private readonly FileSystemVaultCreator _creator = new();

    public void Dispose() => _vault.Dispose();

    [Fact]
    public async Task CreateAsync_writes_the_campaign_note_and_the_obsidian_folder()
    {
        var result = await _creator.CreateAsync(_vault.Root, "La Marca del Este", "cosmere");

        (result.Status, File.ReadAllText(_vault.PathOf("La Marca del Este.md")), File.ReadAllText(_vault.PathOf(".obsidian/app.json")))
            .Should().Be((CreateVaultStatus.Created,
                "---\ntype: campaign\nname: \"La Marca del Este\"\nsystem: cosmere\n---\n## Resumen\n", "{}"));
    }

    [Fact]
    public async Task CreateAsync_makes_a_folder_that_Classify_calls_Vault()
    {
        await _creator.CreateAsync(_vault.Root, "La Marca del Este", "dnd5e");

        new DirectoryBrowser().Classify(_vault.Root).Should().Be(FolderVaultStatus.Vault);
    }

    [Fact]
    public async Task CreateAsync_makes_a_vault_that_VaultService_loads()
    {
        await _creator.CreateAsync(_vault.Root, "La Marca del Este", "cosmere");

        await using var service = new FileSystemVaultServiceFactory(NullLoggerFactory.Instance).Create(_vault.Root);
        await service.LoadAsync();

        service.Campaign.SystemId.Should().Be("cosmere");
    }

    [Fact]
    public async Task CreateAsync_does_not_overwrite_an_existing_note()
    {
        File.WriteAllText(_vault.PathOf("La Marca del Este.md"), "mío");

        var result = await _creator.CreateAsync(_vault.Root, "La Marca del Este", "dnd5e");

        (result.Status, File.ReadAllText(_vault.PathOf("La Marca del Este.md")), Directory.Exists(_vault.PathOf(".obsidian")))
            .Should().Be((CreateVaultStatus.NoteExists, "mío", false));
    }

    [Fact]
    public async Task CreateAsync_does_not_touch_an_existing_obsidian_folder()
    {
        Directory.CreateDirectory(_vault.PathOf(".obsidian"));

        var result = await _creator.CreateAsync(_vault.Root, "La Marca del Este", "dnd5e");

        (result.Status, File.Exists(_vault.PathOf("La Marca del Este.md")))
            .Should().Be((CreateVaultStatus.ObsidianExists, false));
    }

    [Fact]
    public async Task CreateAsync_with_a_name_of_only_invalid_chars_writes_nothing()
    {
        var result = await _creator.CreateAsync(_vault.Root, "???", "dnd5e");

        (result.Status, Directory.GetFileSystemEntries(_vault.Root).Length).Should().Be((CreateVaultStatus.InvalidName, 0));
    }

    [Fact]
    public async Task CreateAsync_in_a_missing_folder_is_FolderMissing()
    {
        var result = await _creator.CreateAsync(_vault.PathOf("no-existe"), "X", "dnd5e");

        result.Status.Should().Be(CreateVaultStatus.FolderMissing);
    }

    [Fact]
    public async Task CreateAsync_in_a_drive_root_is_refused()
    {
        var result = await _creator.CreateAsync(Path.GetPathRoot(Path.GetTempPath())!, "X", "dnd5e");

        result.Status.Should().Be(CreateVaultStatus.DriveRoot);
    }

    [Fact]
    public async Task CreateAsync_twice_on_the_same_folder_fails_the_second_time()
    {
        await _creator.CreateAsync(_vault.Root, "La Marca del Este", "cosmere");
        var first = File.ReadAllText(_vault.PathOf("La Marca del Este.md"));

        var second = await _creator.CreateAsync(_vault.Root, "La Marca del Este", "dnd5e");

        (second.Status, File.ReadAllText(_vault.PathOf("La Marca del Este.md"))).Should().Be((CreateVaultStatus.NoteExists, first));
    }
}
