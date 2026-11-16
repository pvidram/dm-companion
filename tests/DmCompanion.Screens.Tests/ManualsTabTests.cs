using AngleSharp.Dom;
using Bunit;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.Screens.Tests;

public sealed class ManualsTabTests : ManualsUiTestBase
{
    private IRenderedComponent<GlossaryPanel> OpenPanel()
    {
        var cut = Render<GlossaryPanel>();
        Shell.OpenGlossary();
        return cut;
    }

    private static IElement Tab(IRenderedComponent<GlossaryPanel> cut, string label) =>
        cut.FindAll("button[role=tab]").First(b => b.TextContent.Trim() == label);

    [Fact]
    public void Tabs_switch_between_glossary_and_manuals()
    {
        var cut = OpenPanel();

        Tab(cut, "Manuales").Click();

        new
        {
            Selected = cut.FindAll("button[role=tab][aria-selected=true]").Select(b => b.TextContent.Trim()).ToArray(),
            ShowsGlossarySearch = cut.FindAll("input[type=search]").Count,
            Tab = Shell.GlossaryTab,
        }.Should().BeEquivalentTo(new { Selected = new[] { "Manuales" }, ShowsGlossarySearch = 0, Tab = GlossaryTab.Manuals });
    }

    [Fact]
    public void Glossary_tab_keeps_the_glossary_content()
    {
        var cut = OpenPanel();

        cut.FindAll("input[type=search]").Count.Should().Be(1);
    }

    [Fact]
    public async Task Lists_manuals_grouped()
    {
        var folder = Path.Combine(VaultRoot, "manuales", "dnd5e", "Aventuras");
        Directory.CreateDirectory(folder);
        File.Copy(Path.Combine(VaultRoot, PdfPath), Path.Combine(folder, "Mina perdida.pdf"));
        await Services.GetRequiredService<ManualLibrary>().RefreshAsync();
        var cut = OpenPanel();

        Tab(cut, "Manuales").Click();

        new
        {
            Headings = cut.FindAll(".manuals-tab__group").Select(h => h.TextContent.Trim()).ToArray(),
            Rows = cut.FindAll(".manuals-tab button.ds-list-row").Select(b => b.TextContent.Trim()).ToArray(),
        }.Should().BeEquivalentTo(
            new { Headings = new[] { "Aventuras" }, Rows = new[] { "Manual de prueba", "Mina perdida" } },
            options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task Empty_folder_shows_hint()
    {
        File.Delete(Path.Combine(VaultRoot, PdfPath));
        await Services.GetRequiredService<ManualLibrary>().RefreshAsync();
        var cut = OpenPanel();

        Tab(cut, "Manuales").Click();

        cut.Find(".manuals-tab__empty").TextContent.Trim().Should().Be("Pon tus manuales en PDF en manuales/dnd5e/ dentro del vault");
    }

    [Fact]
    public async Task Without_a_system_the_hint_shows_the_id_placeholder_once_encoded()
    {
        await Services.GetRequiredService<VaultHost>().ForgetAsync();
        await Services.GetRequiredService<ManualLibrary>().RefreshAsync();
        var cut = Render<ManualsTab>();

        cut.Find(".manuals-tab__empty").TextContent.Trim().Should().Be("Pon tus manuales en PDF en manuales/<id>/ dentro del vault");
    }

    [Fact]
    public void Selecting_opens_the_viewer()
    {
        SetupOpen(new ManualDocInfo(12, [], true));
        var cut = OpenPanel();
        Tab(cut, "Manuales").Click();

        cut.Find(".manuals-tab button.ds-list-row").Click();

        new { Path = Shell.OpenManualPath, Viewer = cut.FindAll(".manual-viewer").Count }
            .Should().Be(new { Path = (string?)PdfPath, Viewer = 1 });
    }

    [Fact]
    public async Task Deleted_pdf_while_open_returns_to_the_list_with_a_notice()
    {
        SetupOpen(new ManualDocInfo(12, [], true));
        var cut = OpenPanel();
        Tab(cut, "Manuales").Click();
        cut.Find(".manuals-tab button.ds-list-row").Click();

        File.Delete(Path.Combine(VaultRoot, PdfPath));
        await Services.GetRequiredService<ManualLibrary>().RefreshAsync();

        cut.WaitForAssertion(() => new
        {
            Path = Shell.OpenManualPath,
            Notice = cut.FindAll(".manuals-tab .ds-notice__body").Select(n => n.TextContent.Trim()).FirstOrDefault(),
        }.Should().Be(new { Path = (string?)null, Notice = (string?)"El manual ya no está en el vault." }));
    }

    [Theory]
    [InlineData(false, "ds-parchment--wide")]
    [InlineData(true, "ds-parchment--full")]
    public void Expanded_viewer_widens_the_panel(bool expanded, string sizeClass)
    {
        SetupOpen(new ManualDocInfo(12, [], true));
        var cut = OpenPanel();
        Shell.OpenManual(PdfPath);

        if (expanded) Shell.ToggleManualViewerExpanded();

        cut.WaitForAssertion(() => cut.Find("section.glossary-panel").ClassList.Should().Contain(sizeClass));
    }
}
