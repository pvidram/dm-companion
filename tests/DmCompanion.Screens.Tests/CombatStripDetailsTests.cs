using System.Reflection;
using Bunit;
using DmCompanion.Core.Combat;
using DmCompanion.Core.Maps;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Services;

namespace DmCompanion.Screens.Tests;

/// <summary>Detalles de la tira: HP sin máximo, iniciativa tecleada, índices obsoletos y selección al terminar.</summary>
public sealed class CombatStripDetailsTests : CombatUiTestBase
{
    private async Task<IRenderedComponent<DmShell>> RenderWithSpareAsync() =>
        await RenderAsync(more: GenericToken("c", "Cora", hp: 20));

    [Fact]
    public async Task Row_without_known_max_shows_the_current_hp()
    {
        var cut = await RenderAsync(NoteToken("a", "Ana", "Notas/sin-nota.md") with { HpCurrent = 6 });

        cut.FindAll("[data-testid=strip-hp]")[0].TextContent.Should().Be("6");
    }

    [Fact]
    public async Task Add_accepts_a_negative_initiative()
    {
        var cut = await RenderWithSpareAsync();
        await cut.InvokeAsync(() => cut.Find("[data-testid=strip-add]").Click());

        await cut.InvokeAsync(() => cut.Find("[data-testid=add-initiative]").Input("-1"));
        await cut.InvokeAsync(() => cut.Find("[data-testid=add-token-c]").Click());

        Combat.State!.Entries.Last().Initiative.Should().Be(-1);
    }

    [Fact]
    public async Task Add_with_non_numeric_text_shows_an_error()
    {
        var cut = await RenderWithSpareAsync();
        await cut.InvokeAsync(() => cut.Find("[data-testid=strip-add]").Click());

        // Find y acción juntos y con reintento: un re-render tardío entre ambos deja el manejador encontrado obsoleto.
        cut.WaitForAssertion(() => cut.Find("[data-testid=add-initiative]").Input("abc"));
        cut.WaitForAssertion(() => cut.Find("[data-testid=add-token-c]").Click());

        cut.WaitForAssertion(() => cut.Find("[data-testid=add-initiative-error]").TextContent.Should().Be("Escribe un número entero"));
    }

    [Fact]
    public async Task Add_with_non_numeric_text_does_not_add_the_entry()
    {
        var cut = await RenderWithSpareAsync();
        await cut.InvokeAsync(() => cut.Find("[data-testid=strip-add]").Click());

        await cut.InvokeAsync(() => cut.Find("[data-testid=add-initiative]").Input("abc"));
        await cut.InvokeAsync(() => cut.Find("[data-testid=add-token-c]").Click());

        Combat.State!.Entries.Should().HaveCount(2);
    }

    [Fact]
    public async Task Collapsed_next_button_is_disabled_in_read_only()
    {
        var cut = await RenderAsync();
        MakeReadOnly();
        await cut.InvokeAsync(() => cut.Find("[data-testid=strip-toggle]").Click());

        cut.Find("[data-testid=next-turn]").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public async Task Opening_a_card_for_a_stale_index_does_nothing()
    {
        var cut = await RenderAsync();
        await cut.InvokeAsync(() => cut.Find("[data-testid=strip-toggle]").Click());
        var strip = cut.FindComponent<CombatStripBar>().Instance;

        var open = async () => await (Task)typeof(CombatStripBar).GetMethod("OpenCardAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(strip, [new StripTap(99, 0, 0)])!;

        await open.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Ending_combat_clears_the_selected_token()
    {
        var cut = await RenderAsync();
        await cut.InvokeAsync(() => Shell.SelectCombatToken("a"));

        await cut.InvokeAsync(() => cut.Find("[data-testid=strip-end]").Click());
        await cut.InvokeAsync(() => cut.FindAll(".ds-dialog-backdrop:not([hidden]) .ds-button--damage").Single().Click());

        Shell.SelectedCombatTokenId.Should().BeNull();
    }
}
