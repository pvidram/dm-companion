using System.Text.RegularExpressions;
using DmCompanion.Screens.Platform;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.DevHost.Tests;

public sealed class ManualStylesheetTests : IDisposable
{
    private readonly TempVault _vault = TempVault.FromDemo();
    private readonly WebApplicationFactory<Program> _factory;

    public ManualStylesheetTests() =>
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("DevHost:VaultPath", _vault.Root);
            builder.ConfigureTestServices(services => services.AddSingleton<IAppPreferences, InMemoryAppPreferences>());
        });

    public void Dispose()
    {
        _factory.Dispose();
        _vault.Dispose();
    }

    [Fact]
    public async Task Crop_overlay_has_a_visible_veil()
    {
        var rule = await RuleAsync(".manual-crop");

        rule.Should().Contain("background");
    }

    [Fact]
    public async Task Crop_rectangle_has_a_dashed_accent_border_and_a_translucent_fill()
    {
        var rule = await RuleAsync(".manual-crop-rect");

        rule.Should().Contain("border: 2px dashed var(--accent-on-sheet)").And.Contain("color-mix(in srgb, var(--accent-on-sheet)");
    }

    private async Task<string> RuleAsync(string selector)
    {
        using var client = _factory.CreateClient();
        var css = await client.GetStringAsync("/_content/DmCompanion.Screens/css/screens.css");
        var match = Regex.Match(css, @"(?m)^" + Regex.Escape(selector) + @"\s*\{([^}]*)\}");
        return match.Success ? match.Groups[1].Value : "";
    }
}
