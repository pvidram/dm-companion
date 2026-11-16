using AngleSharp.Dom;
using Bunit;
using DmCompanion.Core.Manuals;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Services;
using Microsoft.JSInterop;

namespace DmCompanion.Screens.Tests;

public sealed class ManualSearchPaneTests : ManualsUiTestBase
{
    private static readonly ManualDocInfo Doc = new(12, [], true);

    private IRenderedComponent<ManualViewer> OpenSearch(ManualDocInfo? info = null)
    {
        SetupOpen(info ?? Doc);
        Shell.OpenManual(PdfPath);
        var cut = Render<ManualViewer>(p => p.Add(c => c.Path, PdfPath));
        cut.WaitForAssertion(() => cut.Find("input[aria-label='Página']"));
        cut.FindAll("button").First(b => b.TextContent.Trim() == "Buscar").Click();
        return cut;
    }

    private static string[] Rows(IRenderedComponent<ManualViewer> cut) =>
        cut.FindAll(".manual-search button.ds-list-row").Select(b => b.TextContent.Trim()).ToArray();

    private ManualSearchSession SessionOf(int index) =>
        ((DotNetObjectReference<ManualSearchSession>)Module.Invocations["search"][index].Arguments[1]!).Value;

    private Task Hit(IRenderedComponent<ManualViewer> cut, int page, string snippet) =>
        cut.InvokeAsync(() => SessionOf(0).OnHitAsync(page, snippet));

    private static void Type(IRenderedComponent<ManualViewer> cut, string text) => cut.Find(".manual-search input[type=search]").Input(text);

    [Fact]
    public async Task Search_lists_hits_as_they_arrive()
    {
        var cut = OpenSearch();
        Module.Setup<bool>("search", _ => true);

        Type(cut, "dragón");
        cut.WaitForAssertion(() => Module.Invocations["search"].Count.Should().Be(1));
        await Hit(cut, 3, "un dragón rojo");
        await Hit(cut, 7, "<b>otro</b> dragón");

        // Los repintados se agrupan cada 100 ms como mucho: el segundo acierto llega en el siguiente.
        cut.WaitForAssertion(() => new { Rows = Rows(cut), Html = cut.FindAll(".manual-search b").Count }
            .Should().BeEquivalentTo(new { Rows = new[] { "p. 3 · un dragón rojo", "p. 7 · <b>otro</b> dragón" }, Html = 0 }));
    }

    [Fact]
    public void New_query_cancels_previous()
    {
        var cut = OpenSearch();
        Module.Setup<bool>("search", _ => true);
        Type(cut, "dra");
        cut.WaitForAssertion(() => Module.Invocations["search"].Count.Should().Be(1));
        var first = SessionOf(0);

        Type(cut, "drag");
        cut.WaitForAssertion(() => Module.Invocations["search"].Count.Should().Be(2));

        new { FirstCancelled = first.IsCancelled(), SecondCancelled = SessionOf(1).IsCancelled() }
            .Should().BeEquivalentTo(new { FirstCancelled = true, SecondCancelled = false });
    }

    [Fact]
    public void Closing_cancels_search()
    {
        var cut = OpenSearch();
        Module.Setup<bool>("search", _ => true);
        Type(cut, "dra");
        cut.WaitForAssertion(() => Module.Invocations["search"].Count.Should().Be(1));
        var session = SessionOf(0);

        cut.FindAll("button").First(b => b.TextContent.Trim() == "Buscar").Click();

        session.IsCancelled().Should().BeTrue();
    }

    [Fact]
    public void No_text_message()
    {
        var cut = OpenSearch(new ManualDocInfo(12, [], false));

        cut.Find(".manual-search").TextContent.Should().Contain("Este manual no tiene texto que buscar");
    }

    [Fact]
    public async Task Hit_jumps_to_page()
    {
        var cut = OpenSearch();
        Module.Setup<bool>("search", _ => true);
        Type(cut, "dragón");
        cut.WaitForAssertion(() => Module.Invocations["search"].Count.Should().Be(1));
        await Hit(cut, 7, "un dragón");

        cut.Find(".manual-search button.ds-list-row").Click();

        cut.Find("input[aria-label='Página']").GetAttribute("value").Should().Be("7");
    }

    [Fact]
    public async Task Hit_limit_shows_notice()
    {
        var cut = OpenSearch();
        Module.Setup<bool>("search", _ => true);
        Type(cut, "el");
        cut.WaitForAssertion(() => Module.Invocations["search"].Count.Should().Be(1));
        var session = SessionOf(0);
        for (var i = 1; i <= 500; i++) await cut.InvokeAsync(() => session.OnHitAsync(1 + i % 12, "el"));

        cut.WaitForAssertion(() => cut.Find(".manual-search").TextContent.Should().Contain("Se muestran los primeros 500 resultados"));
    }

    [Fact]
    public async Task A_burst_of_hits_is_rendered_in_batches()
    {
        var cut = OpenSearch();
        Module.Setup<bool>("search", _ => true);
        Type(cut, "el");
        cut.WaitForAssertion(() => Module.Invocations["search"].Count.Should().Be(1));
        var pane = cut.FindComponent<ManualSearchPane>();
        var session = SessionOf(0);
        var before = pane.RenderCount;

        await cut.InvokeAsync(async () =>
        {
            for (var i = 0; i < 60; i++) await session.OnHitAsync(1 + i % 12, "el");
        });

        (pane.RenderCount - before).Should().BeLessThanOrEqualTo(4);
    }

    [Fact]
    public async Task Finishing_the_search_renders_the_pending_hits()
    {
        var cut = OpenSearch();
        var search = Module.Setup<bool>("search", _ => true);
        Type(cut, "el");
        cut.WaitForAssertion(() => Module.Invocations["search"].Count.Should().Be(1));
        var session = SessionOf(0);
        await cut.InvokeAsync(async () =>
        {
            for (var i = 0; i < 5; i++) await session.OnHitAsync(1 + i, "el");
        });

        await cut.InvokeAsync(() => search.SetResult(true));

        cut.WaitForAssertion(() => Rows(cut).Should().HaveCount(5));
    }
}
