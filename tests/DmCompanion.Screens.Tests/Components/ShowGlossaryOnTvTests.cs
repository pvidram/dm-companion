using AngleSharp.Dom;
using Bunit;
using DmCompanion.Core.Tv;
using DmCompanion.Core.Vault.Storage;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Platform;
using DmCompanion.Screens.Services;
using DmCompanion.Screens.Tests.Support;
using DmCompanion.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Screens.Tests;

public sealed class ShowGlossaryOnTvTests : BunitContext, IAsyncLifetime
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    private readonly TempVault _vault = TempVault.FromDemo();
    private readonly FakeTvServer _server = new();

    public ShowGlossaryOnTvTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<IAppPreferences>(new InMemoryAppPreferences());
        Services.AddSingleton<IVaultServiceFactory>(new FileSystemVaultServiceFactory(NullLoggerFactory.Instance));
        Services.AddSingleton<VaultHost>();
        Services.AddSingleton<VaultFileUrls>();
        Services.AddSingleton<NoteImageResolver>();
        Services.AddSingleton<PortraitUrlProvider>();
        Services.AddScoped<ShellState>();
        Services.AddGlossaryForTests();
        Services.AddSingleton(TimeProvider.System);
        Services.AddSingleton<MapSession>();
        Services.AddSingleton<TokenHitPoints>();
        Services.AddSingleton<ITvServer>(_server);
        Services.AddSingleton<TvMapFeed>();
        Services.AddSingleton<TvContentFeed>();
        Services.AddSingleton<TvSession>();
    }

    private VaultHost Host => Services.GetRequiredService<VaultHost>();
    private TvSession Tv => Services.GetRequiredService<TvSession>();

    Task IAsyncLifetime.InitializeAsync() => Host.OpenAsync(_vault.Root);

    async Task IAsyncLifetime.DisposeAsync()
    {
        await Host.DisposeAsync();
        await base.DisposeAsync();
        _vault.Dispose();
    }

    private IRenderedComponent<ShowGlossaryOnTvButton> Render(string key) =>
        Render<ShowGlossaryOnTvButton>(p => p.Add(c => c.Entry, Services.GetRequiredService<GlossaryService>().GetSrd(key)!));

    private static IElement? ShowButton(IRenderedComponent<ShowGlossaryOnTvButton> cut) =>
        cut.FindAll("button").FirstOrDefault(b => b.TextContent.Trim() == "Mostrar en TV");

    [Fact]
    public void Button_hidden_with_tv_off() => ShowButton(Render("srd:conjuros/fireball")).Should().BeNull();

    [Fact]
    public async Task Button_hidden_for_creatures()
    {
        await Tv.EnableAsync();

        ShowButton(Render("srd:criaturas/aboleth")).Should().BeNull();
    }

    [Fact]
    public async Task Button_sends_the_entry()
    {
        await Tv.EnableAsync();
        var cut = Render("srd:conjuros/fireball");

        await cut.InvokeAsync(() => ShowButton(cut)!.Click());

        cut.WaitForAssertion(() => (Tv.Mode, Tv.ContentTitle).Should().Be((TvMode.Content, "Bola de fuego")), Wait);
    }
}
