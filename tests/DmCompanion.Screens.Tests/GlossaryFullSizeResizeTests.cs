using Bunit;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Services;
using DmCompanion.Screens.Tests.Support;
using DmCompanion.UI.Interop;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.Screens.Tests;

public sealed class GlossaryFullSizeResizeTests : ManualsUiTestBase
{
    private IRenderedComponent<GlossaryPanel> RenderExpandedGlossary()
    {
        var store = Services.GetRequiredService<PanelLayoutStore>();
        store.SetSize(LayoutKeys.GlossarySide, new WindowSize(300, 300));
        store.SetSize(LayoutKeys.GlossaryWide, new WindowSize(700, 500));
        SetupOpen(new ManualDocInfo(12, [], true));
        var cut = Render<GlossaryPanel>();
        cut.InvokeAsync(() =>
        {
            Shell.OpenGlossary();
            Shell.OpenManual(PdfPath);
            Shell.ToggleManualViewerExpanded();
        }).GetAwaiter().GetResult();
        cut.WaitForAssertion(() => cut.Find("section.glossary-panel").ClassList.Should().Contain("ds-parchment--full"));
        return cut;
    }

    [Fact]
    public void The_full_size_glossary_has_no_resize_handles()
    {
        var cut = RenderExpandedGlossary();

        cut.FindAll("section.glossary-panel [data-resize-handle]").Should().BeEmpty();
    }

    [Fact]
    public void Going_full_size_releases_the_stored_size_frame()
    {
        RenderExpandedGlossary();

        Services.GetRequiredService<FakeWindowFrameInterop>().Detached.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Collapsing_the_viewer_reattaches_the_wide_frame_with_its_stored_size()
    {
        var cut = RenderExpandedGlossary();
        var frames = Services.GetRequiredService<FakeWindowFrameInterop>();
        var attached = frames.Attached.Count;

        await cut.InvokeAsync(Shell.ToggleManualViewerExpanded);

        cut.WaitForAssertion(() => frames.Attached.Skip(attached).Select(o => o.Size).Should().Contain(new WindowSize(700, 500)));
    }
}
