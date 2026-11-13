using DmCompanion.Core.Maps;
using DmCompanion.Core.Vault;
using DmCompanion.Core.Vault.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Infrastructure.Tests;

public class SampleVaultMapTests : IDisposable
{
    private const string StatePath = "Mapas/Boceto de la cueva.state.json";

    private readonly TempVault _vault = TempVault.FromDemo();

    public void Dispose() => _vault.Dispose();

    [Fact]
    public void Sample_state_loads_without_being_read_only() =>
        MapStateSerializer.Read(File.ReadAllText(_vault.PathOf(StatePath))).Status.Should().Be(MapStateReadStatus.Ok);

    [Fact]
    public void Sample_state_has_two_strokes_and_two_tokens()
    {
        var state = MapStateSerializer.Read(File.ReadAllText(_vault.PathOf(StatePath))).State;

        (state.Strokes.Count, state.Tokens.Count).Should().Be((2, 2));
    }

    [Fact]
    public void Sample_state_has_a_note_token_for_lyra_and_a_generic_one()
    {
        var tokens = MapStateSerializer.Read(File.ReadAllText(_vault.PathOf(StatePath))).State.Tokens;

        tokens.Select(t => (t.Source, t.Side)).Should().Equal(("Personajes/Lyra.md", TokenSide.Pc), (null, TokenSide.Enemy));
    }

    [Fact]
    public void Sample_state_is_written_exactly_as_the_app_would_write_it()
    {
        var json = File.ReadAllText(_vault.PathOf(StatePath));

        MapStateSerializer.Write(MapStateSerializer.Read(json).State).Should().Be(json.Trim());
    }

    [Fact]
    public async Task Short_link_to_faro_viejo_still_points_to_the_location_note()
    {
        var index = await LoadIndexAsync();

        index.Resolve("Faro Viejo")!.RelativePath.Should().Be("Lugares/Faro Viejo.md");
    }

    [Fact]
    public async Task Path_link_to_the_faro_map_points_to_the_map_note()
    {
        var index = await LoadIndexAsync();

        index.Resolve("Mapas/Plano del Faro")!.RelativePath.Should().Be("Mapas/Plano del Faro.md");
    }

    [Fact]
    public async Task Sample_vault_has_a_campaign_note_for_dnd5e()
    {
        await using var service = new VaultService(new FileSystemVaultStorage(_vault.Root), new ManualChangeSource(), NullLogger<VaultService>.Instance);
        await service.LoadAsync();

        service.ActiveSystem.Id.Should().Be("dnd5e");
    }

    private async Task<VaultIndex> LoadIndexAsync()
    {
        await using var service = new VaultService(new FileSystemVaultStorage(_vault.Root), new ManualChangeSource(), NullLogger<VaultService>.Instance);
        await service.LoadAsync();
        return service.Index;
    }
}
