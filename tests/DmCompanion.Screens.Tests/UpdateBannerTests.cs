using Bunit;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Platform;
using DmCompanion.Screens.Services;
using DmCompanion.Screens.Tests.Support;
using DmCompanion.UI;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.Screens.Tests;

public sealed class UpdateBannerTests : BunitContext
{
    private readonly InMemoryAppPreferences _preferences = new();
    private readonly FakeUpdateCheck _check = new() { Latest = new AvailableUpdate("0.3.0", "https://example.test/r") };

    public UpdateBannerTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddDmCompanionUI();
    }

    private async Task<UpdateNotice> NoticeWithPendingAsync()
    {
        var notice = new UpdateNotice(_preferences, _check);
        Services.AddSingleton(notice);
        await notice.RefreshAsync("0.2.0", CancellationToken.None);
        return notice;
    }

    [Fact]
    public async Task Shows_the_new_version()
    {
        await NoticeWithPendingAsync();

        Render<UpdateBanner>().Markup.Should().Contain("Hay una versión nueva (0.3.0)");
    }

    [Fact]
    public void Renders_nothing_without_update_notice() =>
        Render<UpdateBanner>().Markup.Trim().Should().BeEmpty();

    [Fact]
    public async Task Download_opens_the_release_page()
    {
        await NoticeWithPendingAsync();
        var banner = Render<UpdateBanner>();

        banner.FindAll("button").Single(b => b.TextContent.Contains("Descargar")).Click();

        _check.Opened.Should().ContainSingle();
    }

    [Fact]
    public async Task Not_now_hides_the_banner()
    {
        await NoticeWithPendingAsync();
        var banner = Render<UpdateBanner>();

        banner.FindAll("button").Single(b => b.TextContent.Contains("Ahora no")).Click();

        banner.WaitForAssertion(() => banner.Markup.Should().NotContain("Hay una versión nueva"));
    }

    [Fact]
    public async Task Skip_stores_the_version()
    {
        await NoticeWithPendingAsync();
        var banner = Render<UpdateBanner>();

        banner.FindAll("button").Single(b => b.TextContent.Contains("No avisar de esta versión")).Click();

        _preferences.Get(UpdateNotice.SkipKey).Should().Be("0.3.0");
    }

    [Fact]
    public async Task Appears_when_the_notice_arrives_after_rendering()
    {
        var notice = new UpdateNotice(_preferences, _check);
        Services.AddSingleton(notice);
        var banner = Render<UpdateBanner>();

        await banner.InvokeAsync(() => notice.RefreshAsync("0.2.0", CancellationToken.None));

        banner.WaitForAssertion(() => banner.Markup.Should().Contain("Hay una versión nueva (0.3.0)"));
    }
}
