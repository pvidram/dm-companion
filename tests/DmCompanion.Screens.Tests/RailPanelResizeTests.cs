using Bunit;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Services;
using DmCompanion.Screens.Tests.Support;
using DmCompanion.UI.Interop;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.Screens.Tests;

public sealed class RailPanelResizeTests : TvPanelTestBase
{
    public static TheoryData<OverlayPanel, string> Panels => new()
    {
        { OverlayPanel.Notes, LayoutKeys.Notes },
        { OverlayPanel.Players, LayoutKeys.Players },
        { OverlayPanel.Glossary, LayoutKeys.GlossaryWide },
        { OverlayPanel.Maps, LayoutKeys.Maps },
        { OverlayPanel.Sound, LayoutKeys.Sound },
        { OverlayPanel.Tv, LayoutKeys.Tv },
        { OverlayPanel.Vault, LayoutKeys.Vault },
    };

    private PanelLayoutStore Store => Services.GetRequiredService<PanelLayoutStore>();
    private FakeWindowFrameInterop Frames => Services.GetRequiredService<FakeWindowFrameInterop>();
    private ShellState Shell => Services.GetRequiredService<ShellState>();

    private IRenderedComponent<DmShell> RenderOpen(OverlayPanel panel)
    {
        var cut = Render<DmShell>();
        cut.InvokeAsync(() => Shell.Toggle(panel)).GetAwaiter().GetResult();
        cut.WaitForAssertion(() => Frames.Attached.Should().NotBeEmpty(), Wait);
        return cut;
    }

    [Theory]
    [MemberData(nameof(Panels))]
    public void Each_rail_panel_is_resizable(OverlayPanel panel, string key)
    {
        _ = key;
        var cut = RenderOpen(panel);

        cut.FindAll("section.ds-parchment:not([hidden]) [data-resize-handle='se']").Count.Should().Be(1);
    }

    [Fact]
    public void Notes_panel_uses_its_stored_size()
    {
        Store.SetSize(LayoutKeys.Notes, new WindowSize(500, 400));

        RenderOpen(OverlayPanel.Notes);

        Frames.Attached[^1].Size.Should().Be(new WindowSize(500, 400));
    }

    [Theory]
    [MemberData(nameof(Panels))]
    public async Task Resizing_saves_the_size_under_the_panel_key(OverlayPanel panel, string key)
    {
        var cut = RenderOpen(panel);

        await cut.InvokeAsync(() => Frames.Listener!.OnFrameChangedAsync(new WindowFrameChange(0, 0, new WindowSize(610, 480), null)));

        Store.GetSize(key).Should().Be(new WindowSize(610, 480));
    }

    [Fact]
    public void Glossary_wide_applies_its_own_stored_size()
    {
        Store.SetSize(LayoutKeys.GlossarySide, new WindowSize(300, 300));
        Store.SetSize(LayoutKeys.GlossaryWide, new WindowSize(700, 500));

        RenderOpen(OverlayPanel.Glossary);

        Frames.Attached[^1].Size.Should().Be(new WindowSize(700, 500));
    }

    [Fact]
    public async Task Resizing_the_wide_glossary_updates_only_the_wide_key()
    {
        Store.SetSize(LayoutKeys.GlossarySide, new WindowSize(300, 300));
        var cut = RenderOpen(OverlayPanel.Glossary);

        await cut.InvokeAsync(() => Frames.Listener!.OnFrameChangedAsync(new WindowFrameChange(0, 0, new WindowSize(720, 510), null)));

        (Store.GetSize(LayoutKeys.GlossaryWide), Store.GetSize(LayoutKeys.GlossarySide))
            .Should().Be((new WindowSize(720, 510), new WindowSize(300, 300)));
    }

    [Fact]
    public async Task Reset_returns_to_the_default_size()
    {
        Store.SetSize(LayoutKeys.Notes, new WindowSize(500, 400));
        var cut = RenderOpen(OverlayPanel.Notes);

        await cut.InvokeAsync(() => Frames.Listener!.OnFrameResetAsync());

        cut.WaitForAssertion(() => (Store.GetSize(LayoutKeys.Notes), Frames.Updated[^1].Size).Should().Be(((WindowSize?)null, (WindowSize?)null)), Wait);
    }
}
