using DmCompanion.Core.Tv;
using DmCompanion.Screens.Services;

namespace DmCompanion.Screens.Tests;

/// <summary>Cambiar de vault con la TV mostrando un mapa o un contenido: vuelve a la espera y deja de servir los ficheros viejos.</summary>
public sealed class TvSessionVaultSwitchTests : TvSessionTestBase, IDisposable
{
    private readonly TempVault _other = TempVault.Empty();

    private string NewCampaign => Path.GetFileName(_other.Root);

    void IDisposable.Dispose() => _other.Dispose();

    private async Task<IReadOnlyDictionary<string, TvFile>> SwitchVaultAsync()
    {
        var oldFiles = Server.Files;
        await Host.OpenAsync(_other.Root);
        await WaitUntilAsync(() => LastKind == "waiting" && LastMessage.GetProperty("campaign").GetString() == NewCampaign);
        return oldFiles;
    }

    private async Task ShowLighthouseMapAsync()
    {
        await Maps.OpenAsync(Lighthouse);
        await Tv.FixFrameAsync(Lighthouse, Square);
    }

    [Fact]
    public async Task Switching_vault_from_the_map_goes_back_to_waiting_with_the_new_campaign()
    {
        await ShowLighthouseMapAsync();

        await SwitchVaultAsync();

        (Tv.Mode, Tv.MapPath, Server.Files.Count).Should().Be((TvMode.Waiting, (string?)null, 0));
    }

    [Fact]
    public async Task Switching_vault_from_the_map_stops_serving_its_files()
    {
        await ShowLighthouseMapAsync();

        var oldFiles = await SwitchVaultAsync();

        (await IsServedAsync(oldFiles.Values.Single())).Should().BeFalse();
    }

    [Fact]
    public async Task Switching_vault_from_content_goes_back_to_waiting_not_to_the_old_map()
    {
        await ShowLighthouseMapAsync();
        await Tv.ShowContentAsync(OldLighthouse);

        await SwitchVaultAsync();

        (Tv.Mode, Tv.ContentPath, Tv.LastMap, Server.Files.Count).Should().Be((TvMode.Waiting, (string?)null, (TvMapMessage?)null, 0));
    }

    [Fact]
    public async Task Switching_vault_from_content_stops_serving_its_files()
    {
        await Tv.ShowContentAsync("Personajes/Lyra.md");

        var oldFiles = await SwitchVaultAsync();

        var served = await Task.WhenAll(oldFiles.Values.Select(IsServedAsync));
        served.Should().NotBeEmpty().And.AllBeEquivalentTo(false);
    }

    private static async Task<bool> IsServedAsync(TvFile file)
    {
        try
        {
            await using var stream = await file.Open(CancellationToken.None);
            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
    }
}
