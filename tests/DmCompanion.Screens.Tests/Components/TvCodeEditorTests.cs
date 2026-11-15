using AngleSharp.Dom;
using Bunit;
using DmCompanion.Core.Tv;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Services;

namespace DmCompanion.Screens.Tests;

public sealed class TvCodeEditorTests : TvPanelTestBase
{
    private static IElement CodeInput(IElement panel) => panel.QuerySelector("input")!;

    // Los diálogos siempre están en el DOM y se ocultan con «hidden».
    private static bool CodeConfirmOpen(IRenderedComponent<DmShell> cut) =>
        cut.FindAll(".ds-dialog-backdrop").Single(d => d.TextContent.Contains("dejarán de entrar")).HasAttribute("hidden") == false;

    private const string InvalidText = "Usa de 4 a 32 letras sin tildes, números o guiones.";

    [Fact]
    public void Panel_shows_the_code_editable_while_off()
    {
        var cut = RenderWithPanelOpen();

        (Panel(cut).TextContent.Contains("Código de la TV"), CodeInput(Panel(cut)).GetAttribute("value")).Should().Be((true, "costa-de-sal"));
    }

    [Fact]
    public void Address_uses_the_chosen_code()
    {
        var cut = RenderWithPanelOpen();
        CodeInput(Panel(cut)).Input("BrumaVela");
        cut.InvokeAsync(() => ButtonIn(Panel(cut), "Guardar").Click());
        cut.WaitForAssertion(() => Tv.Code.Should().Be("brumavela"), Wait);

        cut.InvokeAsync(() => ButtonIn(Panel(cut), "Activar TV").Click());

        cut.WaitForAssertion(() => Panel(cut).QuerySelector(".tv-panel__address")!.TextContent.Should().EndWith("/tv/brumavela/"), Wait);
    }

    [Fact]
    public void Invalid_code_shows_the_hint_and_is_not_saved()
    {
        var cut = RenderEnabled();
        CodeInput(Panel(cut)).Input("a_b");

        cut.InvokeAsync(() => ButtonIn(Panel(cut), "Guardar").Click());

        cut.WaitForAssertion(() => (Panel(cut).TextContent.Contains(InvalidText), Tv.Code).Should().Be((true, "costa-de-sal")), Wait);
    }

    [Fact]
    public void Changing_a_used_code_asks_for_confirmation()
    {
        var cut = RenderEnabled();
        CodeInput(Panel(cut)).Input("brumavela");

        cut.InvokeAsync(() => ButtonIn(Panel(cut), "Guardar").Click());

        cut.WaitForAssertion(() => (CodeConfirmOpen(cut), Tv.Code)
            .Should().Be((true, "costa-de-sal")), Wait);
    }

    [Fact]
    public void Confirming_applies_the_new_code_and_cancelling_keeps_the_old_one()
    {
        var cut = RenderEnabled();
        CodeInput(Panel(cut)).Input("brumavela");
        cut.InvokeAsync(() => ButtonIn(Panel(cut), "Guardar").Click());
        cut.WaitForAssertion(() => CodeConfirmOpen(cut).Should().BeTrue(), Wait);
        cut.InvokeAsync(() => cut.FindAll("button").First(b => b.TextContent.Trim() == "Cancelar").Click());
        cut.WaitForAssertion(() => (CodeConfirmOpen(cut), Tv.Code).Should().Be((false, "costa-de-sal")), Wait);

        cut.InvokeAsync(() => ButtonIn(Panel(cut), "Guardar").Click());
        cut.WaitForAssertion(() => CodeConfirmOpen(cut).Should().BeTrue(), Wait);
        cut.InvokeAsync(() => cut.FindAll("button").First(b => b.TextContent.Trim() == "Cambiar").Click());

        cut.WaitForAssertion(() => (Tv.Code, Panel(cut).QuerySelector(".tv-panel__address")!.TextContent.Trim()).Should().Be(("brumavela", Tv.AddressFor("192.168.1.20")!)), Wait);
    }

    [Fact]
    public void Restart_failure_says_the_code_was_saved_but_the_tv_could_not_be_reactivated()
    {
        var cut = RenderEnabled();
        _server.PortsBusy = true;
        CodeInput(Panel(cut)).Input("brumavela");
        cut.InvokeAsync(() => ButtonIn(Panel(cut), "Guardar").Click());
        cut.WaitForAssertion(() => CodeConfirmOpen(cut).Should().BeTrue(), Wait);

        cut.InvokeAsync(() => cut.FindAll("button").First(b => b.TextContent.Trim() == "Cambiar").Click());

        cut.WaitForAssertion(() => (Panel(cut).TextContent.Contains("Código guardado, pero no se pudo reactivar la TV: puertos ocupados."), Tv.Mode, Tv.Code)
            .Should().Be((true, TvMode.Off, "brumavela")), Wait);
    }

    [Fact]
    public void Saving_the_unsaved_suggestion_as_typed_saves_it()
    {
        var cut = RenderWithPanelOpen();

        cut.InvokeAsync(() => ButtonIn(Panel(cut), "Guardar").Click());

        cut.WaitForAssertion(() => Tv.HasSavedCode.Should().BeTrue(), Wait);
    }

    [Fact]
    public void Draft_survives_toggling_the_tv()
    {
        var cut = RenderWithPanelOpen();
        CodeInput(Panel(cut)).Input("borrador");

        cut.InvokeAsync(() => ButtonIn(Panel(cut), "Activar TV").Click());
        cut.WaitForAssertion(() => Tv.Mode.Should().Be(TvMode.Waiting), Wait);

        cut.WaitForAssertion(() => CodeInput(Panel(cut)).GetAttribute("value").Should().Be("borrador"), Wait);
    }
}
