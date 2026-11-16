using AngleSharp.Dom;
using Bunit;
using DmCompanion.Core.Manuals;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.Screens.Tests;

public sealed class ManualBookmarksPaneTests : ManualsUiTestBase
{
    private static readonly ManualDocInfo Doc = new(12,
    [
        new OutlineEntry("Capítulo 1", 1, 0),
        new OutlineEntry("Combate", 3, 1),
    ], true);

    private string BookmarksFile => Path.Combine(VaultRoot, "manuales", "dnd5e", "Manual de prueba.marcadores.json");

    private IRenderedComponent<ManualViewer> OpenBookmarks(int? lastPage = null, ManualDocInfo? info = null)
    {
        SetupOpen(info ?? Doc);
        if (lastPage is { } page) Prefs.Set(PageKey, page.ToString());
        Shell.OpenManual(PdfPath);
        var cut = Render<ManualViewer>(p => p.Add(c => c.Path, PdfPath));
        cut.WaitForAssertion(() => cut.Find("input[aria-label='Página']"));
        cut.FindAll("button").First(b => b.TextContent.Trim() == "Marcadores").Click();
        return cut;
    }

    private static IElement AddButton(IRenderedComponent<ManualViewer> cut) =>
        cut.FindAll(".manual-bookmarks button").First(b => b.TextContent.Trim() == "Añadir marcador aquí");

    private static string[] Rows(IRenderedComponent<ManualViewer> cut) =>
        cut.FindAll(".manual-bookmarks button.ds-list-row").Select(b => b.TextContent.Trim()).ToArray();

    private static void Save(IRenderedComponent<ManualViewer> cut, string? name = null)
    {
        AddButton(cut).Click();
        if (name is not null) cut.Find(".manual-bookmarks__dialog input").Input(name);
        cut.FindAll(".manual-bookmarks__dialog button").First(b => b.TextContent.Trim() == "Guardar").Click();
    }

    [Fact]
    public void Add_bookmark_suggests_section_name()
    {
        var cut = OpenBookmarks(lastPage: 4);

        AddButton(cut).Click();

        cut.Find(".manual-bookmarks__dialog input").GetAttribute("value").Should().Be("Combate");
    }

    [Fact]
    public void Add_bookmark_suggests_page_name_without_section()
    {
        var cut = OpenBookmarks(lastPage: 2, info: new ManualDocInfo(12, [], true));

        AddButton(cut).Click();

        cut.Find(".manual-bookmarks__dialog input").GetAttribute("value").Should().Be("Página 2");
    }

    [Fact]
    public void Add_and_remove_bookmark_persist()
    {
        var cut = OpenBookmarks(lastPage: 4);

        Save(cut, "Mi marcador");
        cut.WaitForAssertion(() => Rows(cut).Should().Equal("p. 4 · Mi marcador"));
        var afterAdd = File.ReadAllText(BookmarksFile);
        cut.Find(".manual-bookmarks button[aria-label='Borrar marcador']").Click();
        cut.WaitForAssertion(() => Rows(cut).Should().BeEmpty());

        new { Added = afterAdd.Contains("Mi marcador"), Removed = File.ReadAllText(BookmarksFile).Contains("Mi marcador") }
            .Should().BeEquivalentTo(new { Added = true, Removed = false });
    }

    [Fact]
    public void Corrupt_bookmarks_disable_add()
    {
        File.WriteAllText(BookmarksFile, "{ esto no es json");
        var cut = OpenBookmarks();

        cut.WaitForAssertion(() => cut.Find(".manual-bookmarks").TextContent.Should().Contain("El fichero de marcadores está dañado"));

        new { Disabled = AddButton(cut).HasAttribute("disabled"), Untouched = File.ReadAllText(BookmarksFile) }
            .Should().BeEquivalentTo(new { Disabled = true, Untouched = "{ esto no es json" });
    }

    [Fact]
    public void Opening_the_viewer_loads_bookmarks_so_diagnostics_sees_corruption()
    {
        File.WriteAllText(BookmarksFile, "{ esto no es json");
        SetupOpen(Doc);
        Shell.OpenManual(PdfPath);
        var cut = Render<ManualViewer>(p => p.Add(c => c.Path, PdfPath));

        cut.WaitForAssertion(() => cut.Find("input[aria-label='Página']"));
        cut.WaitForAssertion(() => Services.GetRequiredService<ManualBookmarkStore>().CorruptFiles.Should().HaveCount(1));
    }

    [Fact]
    public void Bookmark_row_jumps_to_page()
    {
        var cut = OpenBookmarks(lastPage: 4);
        Save(cut, "Mi marcador");
        cut.WaitForAssertion(() => Rows(cut).Should().HaveCount(1));
        cut.Find("input[aria-label='Página']").Change("9");
        cut.FindAll("button").First(b => b.TextContent.Trim() == "Ir").Click();

        cut.Find(".manual-bookmarks button.ds-list-row").Click();

        cut.Find("input[aria-label='Página']").GetAttribute("value").Should().Be("4");
    }
}
