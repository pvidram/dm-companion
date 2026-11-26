using AngleSharp.Dom;
using Bunit;
using DmCompanion.Core.Manuals;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.Screens.Tests;

public sealed class ManualViewerTests : ManualsUiTestBase
{
    private static readonly ManualDocInfo Doc = new(12,
    [
        new OutlineEntry("Capítulo 1", 1, 0),
        new OutlineEntry("Combate", 3, 1),
        new OutlineEntry("Magia", 6, 0),
        new OutlineEntry("Conjuros", 7, 1),
    ], true);

    private BackNavigation Back => new(Shell);

    private IRenderedComponent<ManualViewer> OpenViewer(ManualDocInfo? info = null, int? lastPage = null)
    {
        SetupOpen(info ?? Doc);
        if (lastPage is { } page) Prefs.Set(PageKey, page.ToString());
        Shell.OpenManual(PdfPath);
        var cut = Render<ManualViewer>(p => p.Add(c => c.Path, PdfPath));
        cut.WaitForAssertion(() => cut.Find("input[aria-label='Página']"));
        return cut;
    }

    private static IElement Button(IRenderedComponent<ManualViewer> cut, string label) =>
        cut.FindAll("button").First(b => b.TextContent.Trim() == label);

    private static string CurrentPage(IRenderedComponent<ManualViewer> cut) => cut.Find("input[aria-label='Página']").GetAttribute("value")!;

    private static string[] OutlineRows(IRenderedComponent<ManualViewer> cut) =>
        cut.FindAll(".manual-outline button.ds-list-row").Select(b => b.TextContent.Trim()).ToArray();

    // El objeto que el visor entrega al JS: con él se simula lo que el JS avisa al desplazarse.
    private ManualViewerCallbacks Callbacks() =>
        ((Microsoft.JSInterop.DotNetObjectReference<ManualViewerCallbacks>)Module.VerifyInvoke("open").Arguments[3]!).Value;

    [Fact]
    public void Opens_at_last_page()
    {
        var cut = OpenViewer(lastPage: 5);

        new { Page = CurrentPage(cut), Open = Module.VerifyInvoke("open").Arguments[2] }
            .Should().BeEquivalentTo(new { Page = "5", Open = (object)5 });
    }

    [Fact]
    public void Opens_at_first_page_without_a_remembered_one()
    {
        var cut = OpenViewer();

        CurrentPage(cut).Should().Be("1");
    }

    [Fact]
    public void Next_and_previous_change_page_and_remember()
    {
        var cut = OpenViewer(lastPage: 5);

        Button(cut, "Siguiente").Click();
        Button(cut, "Siguiente").Click();
        Button(cut, "Anterior").Click();

        new { Page = CurrentPage(cut), Remembered = Prefs.Get(PageKey), Shown = Module.Invocations["showPage"].Select(i => i.Arguments[0]).ToArray() }
            .Should().BeEquivalentTo(new { Page = "6", Remembered = "6", Shown = new object?[] { 6, 7, 6 } });
    }

    [Theory]
    [InlineData("99", "12")]
    [InlineData("0", "1")]
    [InlineData("-4", "1")]
    public void Go_to_page_clamps_to_range(string typed, string expected)
    {
        var cut = OpenViewer(lastPage: 5);
        cut.Find("input[aria-label='Página']").Change(typed);

        Button(cut, "Ir").Click();

        CurrentPage(cut).Should().Be(expected);
    }

    [Fact]
    public void Outline_filter_keeps_parents()
    {
        var cut = OpenViewer();
        Button(cut, "Índice").Click();

        cut.Find(".manual-outline input[type=search]").Input("conjuros");

        OutlineRows(cut).Should().Equal("Magia", "Conjuros");
    }

    [Fact]
    public void Outline_entry_jumps_to_page()
    {
        var cut = OpenViewer();
        Button(cut, "Índice").Click();

        cut.FindAll(".manual-outline button.ds-list-row").First(b => b.TextContent.Trim() == "Combate").Click();

        new { Page = CurrentPage(cut), Remembered = Prefs.Get(PageKey) }.Should().BeEquivalentTo(new { Page = "3", Remembered = "3" });
    }

    [Fact]
    public void No_outline_message()
    {
        var cut = OpenViewer(new ManualDocInfo(12, [], true));

        Button(cut, "Índice").Click();

        cut.Find(".manual-outline").TextContent.Should().Contain("Este PDF no tiene índice");
    }

    [Fact]
    public void Open_failure_shows_message()
    {
        SetupOpen(null);
        Shell.OpenManual(PdfPath);

        var cut = Render<ManualViewer>(p => p.Add(c => c.Path, PdfPath));

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("No se pudo abrir este manual"));
        Button(cut, "Atrás").Should().NotBeNull();
    }

    [Fact]
    public async Task Back_closes_pane_then_viewer()
    {
        var cut = OpenViewer();
        Button(cut, "Índice").Click();

        var first = await Back.HandleBackAsync();
        var afterFirst = (Shell.ActiveManualPane, Shell.OpenManualPath);
        var second = await Back.HandleBackAsync();

        new { first, afterFirst, second, Path = Shell.OpenManualPath, Panel = Shell.ActivePanel }
            .Should().BeEquivalentTo(new
            {
                first = true,
                afterFirst = (ManualPane.None, (string?)PdfPath),
                second = true,
                Path = (string?)null,
                Panel = OverlayPanel.Glossary,
            });
    }

    [Fact]
    public void Closing_viewer_calls_CloseAsync()
    {
        var cut = OpenViewer();

        Button(cut, "Atrás").Click();

        cut.WaitForAssertion(() => Module.Invocations["close"].Count.Should().Be(1));
    }

    [Fact]
    public async Task Closing_while_opening_closes_the_document()
    {
        var pending = Module.Setup<ManualDocInfo?>("open", _ => true);
        Shell.OpenManual(PdfPath);
        var cut = Render<ManualViewer>(p => p.Add(c => c.Path, PdfPath));
        await cut.WaitForStateAsync(() => Module.Invocations["open"].Count == 1);
        Button(cut, "Atrás").Click();
        cut.WaitForAssertion(() => Module.Invocations["close"].Count.Should().Be(1));

        pending.SetResult(Doc);

        cut.WaitForAssertion(() => Module.Invocations["close"].Count.Should().Be(2));
        cut.FindAll("input[aria-label='Página']").Count.Should().Be(0);
    }

    [Fact]
    public void Unmounting_calls_CloseAsync_without_pressing_back()
    {
        SetupOpen(Doc);
        var tab = Render<ManualsTab>();
        Shell.OpenManual(PdfPath);
        tab.WaitForAssertion(() => tab.Find("input[aria-label='Página']"));

        Shell.CloseManual();

        tab.WaitForAssertion(() => Module.Invocations["close"].Count.Should().Be(1));
    }

    [Fact]
    public void Out_of_range_page_resets_the_field_to_the_real_page()
    {
        var cut = OpenViewer(lastPage: 12);
        cut.Find("input[aria-label='Página']").Change("99");

        Button(cut, "Ir").Click();

        CurrentPage(cut).Should().Be("12");
    }

    [Fact]
    public void Page_that_cannot_be_painted_shows_a_notice()
    {
        PageFailsToPaint = true;
        var cut = OpenViewer();

        Button(cut, "Siguiente").Click();

        cut.WaitForAssertion(() => cut.FindAll(".manual-viewer .ds-notice").Select(n => n.TextContent.Trim())
            .Should().Equal("No se pudo mostrar esta página"));
    }

    [Fact]
    public void Painted_page_shows_no_notice()
    {
        var cut = OpenViewer();

        Button(cut, "Siguiente").Click();

        cut.FindAll(".manual-viewer .ds-notice").Should().BeEmpty();
    }

    [Fact]
    public void Expand_toggles_the_viewer_size_without_zooming_the_page()
    {
        var cut = OpenViewer();

        Button(cut, "Ampliar").Click();

        new { Expanded = Shell.ManualViewerExpanded, Label = cut.FindAll("button").Any(b => b.TextContent.Trim() == "Reducir"), Zoomed = Module.Invocations["setZoom"].Count }
            .Should().Be(new { Expanded = true, Label = true, Zoomed = 0 });
    }

    [Fact]
    public void Reduce_returns_the_viewer_to_its_normal_size()
    {
        var cut = OpenViewer();
        Button(cut, "Ampliar").Click();

        Button(cut, "Reducir").Click();

        new { Expanded = Shell.ManualViewerExpanded, Label = cut.FindAll("button").Any(b => b.TextContent.Trim() == "Ampliar") }
            .Should().Be(new { Expanded = false, Label = true });
    }

    [Fact]
    public async Task Navigation_is_disabled_while_cropping()
    {
        var cut = OpenViewer(lastPage: 5);
        var context = cut.FindComponent<ManualTvActions>().Instance.Context!;

        await cut.InvokeAsync(() => context.SetCropping!.Invoke(true));

        cut.WaitForAssertion(() => new[] { "Anterior", "Siguiente", "Ir" }.Select(label => Button(cut, label).HasAttribute("disabled"))
            .Should().AllBeEquivalentTo(true));
    }

    [Fact]
    public async Task Navigation_is_enabled_again_after_the_crop()
    {
        var cut = OpenViewer(lastPage: 5);
        await cut.InvokeAsync(() => cut.FindComponent<ManualTvActions>().Instance.Context!.SetCropping!.Invoke(true));

        await cut.InvokeAsync(() => cut.FindComponent<ManualTvActions>().Instance.Context!.SetCropping!.Invoke(false));

        cut.WaitForAssertion(() => new[] { "Anterior", "Siguiente", "Ir" }.Select(label => Button(cut, label).HasAttribute("disabled"))
            .Should().AllBeEquivalentTo(false));
    }

    [Fact]
    public void Outline_tree_is_built_once_and_kept_across_pages()
    {
        var cut = OpenViewer();
        var before = cut.FindComponent<ManualTvActions>().Instance.Context!.Outline;

        Button(cut, "Siguiente").Click();

        cut.FindComponent<ManualTvActions>().Instance.Context!.Outline.Should().BeSameAs(before);
    }

    [Fact]
    public void Page_changed_updates_the_counter_without_asking_the_viewer_to_scroll()
    {
        var cut = OpenViewer(lastPage: 5);

        Callbacks().OnPageChanged(8);

        cut.WaitForAssertion(() => new { Page = CurrentPage(cut), Shown = Module.Invocations["showPage"].Count }
            .Should().Be(new { Page = "8", Shown = 0 }));
    }

    [Fact]
    public void Page_changed_remembers_the_last_page_read()
    {
        var cut = OpenViewer(lastPage: 5);

        Callbacks().OnPageChanged(8);

        cut.WaitForAssertion(() => Prefs.Get(PageKey).Should().Be("8"));
    }

    [Fact]
    public void Page_changed_beyond_the_last_page_is_clamped()
    {
        var cut = OpenViewer(lastPage: 5);

        Callbacks().OnPageChanged(99);

        cut.WaitForAssertion(() => CurrentPage(cut).Should().Be("12"));
    }

    [Fact]
    public void Page_changed_clears_the_page_notice()
    {
        PageFailsToPaint = true;
        var cut = OpenViewer();
        Button(cut, "Siguiente").Click();
        cut.WaitForAssertion(() => cut.FindAll(".manual-viewer .ds-notice").Should().HaveCount(1));

        Callbacks().OnPageChanged(4);

        cut.WaitForAssertion(() => cut.FindAll(".manual-viewer .ds-notice").Should().BeEmpty());
    }

    [Fact]
    public void Page_changed_with_the_same_page_does_not_save_it_again()
    {
        var cut = OpenViewer(lastPage: 5);
        Prefs.Set(PageKey, "marca");

        Callbacks().OnPageChanged(5);

        cut.WaitForAssertion(() => Prefs.Get(PageKey).Should().Be("marca"));
    }

    [Fact]
    public void Page_changed_after_closing_does_nothing()
    {
        var cut = OpenViewer(lastPage: 5);
        var callbacks = Callbacks();
        Button(cut, "Atrás").Click();
        cut.WaitForAssertion(() => Module.Invocations["close"].Count.Should().Be(1));

        callbacks.OnPageChanged(8);

        cut.WaitForAssertion(() => Prefs.Get(PageKey).Should().Be("5"));
    }

    [Fact]
    public void Page_changed_in_flight_does_not_override_a_navigation_still_pending()
    {
        var cut = OpenViewer(lastPage: 5);
        HoldShowPage = true;
        cut.Find("input[aria-label='Página']").Change("10");
        Button(cut, "Ir").Click();

        Callbacks().OnPageChanged(8);
        HeldShowPage(true);

        cut.WaitForAssertion(() => new { Page = CurrentPage(cut), Saved = Prefs.Get(PageKey) }
            .Should().Be(new { Page = "10", Saved = "10" }));
    }

    [Fact]
    public void Go_to_page_asks_the_viewer_to_scroll_to_the_typed_page()
    {
        var cut = OpenViewer(lastPage: 5);
        cut.Find("input[aria-label='Página']").Change("8");

        Button(cut, "Ir").Click();

        Module.Invocations["showPage"].Select(i => i.Arguments[0]).Should().Equal(8);
    }

    [Fact]
    public void Outline_entry_asks_the_viewer_to_scroll_to_its_page()
    {
        var cut = OpenViewer();
        Button(cut, "Índice").Click();

        cut.FindAll(".manual-outline button.ds-list-row").First(b => b.TextContent.Trim() == "Combate").Click();

        Module.Invocations["showPage"].Select(i => i.Arguments[0]).Should().Equal(3);
    }
}
