using AngleSharp.Dom;
using Bunit;
using DmCompanion.Core.Combat;
using DmCompanion.Core.Maps;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.Screens.Tests;

public sealed class CombatConditionEditorTests : CombatUiTestBase
{
    private Task OpenEditor(IRenderedComponent<DmShell> cut) => cut.InvokeAsync(() => cut.Find("[data-testid=add-condition]").Click());


    [Fact]
    public async Task Condition_editor_lists_the_system_catalog_and_free_text()
    {
        var cut = await RenderAsync();
        await OpenEditor(cut);

        var options = Select(cut, "condition-select").QuerySelectorAll("option").Select(o => o.TextContent).ToList();

        options.Should().Contain(["Asustado", "Agotamiento", "Otro (texto libre)"]);
    }

    [Fact]
    public async Task Free_text_condition_is_added_with_its_label()
    {
        var cut = await RenderAsync();
        await OpenEditor(cut);

        await cut.InvokeAsync(() => Select(cut, "condition-select").Change(Select(cut, "condition-select").QuerySelectorAll("option").Count(o => !o.HasAttribute("hidden")) - 1));
        await cut.InvokeAsync(() => cut.Find("[data-testid=condition-label]").Input("Bendecido"));
        await cut.InvokeAsync(() => cut.Find("[data-testid=condition-save]").Click());

        Ana.ConditionList.Select(c => c.Label).Should().Equal("Bendecido");
    }

    [Fact]
    public async Task Catalog_condition_is_added_with_its_key()
    {
        var cut = await RenderAsync();
        await OpenEditor(cut);

        await cut.InvokeAsync(() => cut.Find("[data-testid=condition-save]").Click());

        Ana.ConditionList.Single().Key.Should().NotBeNull();
    }

    [Fact]
    public async Task Exhaustion_asks_for_level()
    {
        var cut = await RenderAsync();
        await OpenEditor(cut);

        await SelectByLabel(cut, "Agotamiento");

        cut.FindAll("[data-testid=condition-level]").Should().HaveCount(1);
    }

    [Fact]
    public async Task Exhaustion_level_is_capped_to_the_catalog_maximum()
    {
        var cut = await RenderAsync();
        await OpenEditor(cut);
        await SelectByLabel(cut, "Agotamiento");

        await cut.InvokeAsync(() => cut.Find("[data-testid=condition-level] input").Change("9"));
        await cut.InvokeAsync(() => cut.Find("[data-testid=condition-save]").Click());

        Ana.ConditionList.Single().Level.Should().Be(6);
    }

    [Fact]
    public async Task Other_conditions_do_not_ask_for_level()
    {
        var cut = await RenderAsync();
        await OpenEditor(cut);

        cut.FindAll("[data-testid=condition-level]").Should().BeEmpty();
    }

    [Fact]
    public async Task Rounds_field_only_appears_during_combat()
    {
        var cut = await RenderAsync(start: false);
        await cut.InvokeAsync(() => Shell.SelectCombatToken("a"));
        cut.Render();

        cut.FindAll("[data-testid=add-condition]").Should().BeEmpty();
    }

    [Fact]
    public async Task Rounds_field_appears_in_combat()
    {
        var cut = await RenderAsync();
        await OpenEditor(cut);

        cut.FindAll("[data-testid=condition-rounds]").Should().HaveCount(1);
    }

    [Fact]
    public async Task Anchor_selector_defaults_to_the_affected_token()
    {
        var cut = await RenderAsync();
        await cut.InvokeAsync(() => cut.FindAll("[data-testid=strip-select]")[1].Click());
        await OpenEditor(cut);

        var select = Select(cut, "condition-anchor");

        select.QuerySelector("option[selected]")!.TextContent.Should().Be("Bruno");
    }

    [Fact]
    public async Task Condition_with_rounds_is_anchored_to_the_selected_entry()
    {
        var cut = await RenderAsync();
        await cut.InvokeAsync(() => cut.FindAll("[data-testid=strip-select]")[1].Click());
        await OpenEditor(cut);

        await cut.InvokeAsync(() => cut.Find("[data-testid=condition-rounds]").Input("3"));
        await cut.InvokeAsync(() => cut.Find("[data-testid=condition-save]").Click());

        var bruno = Combat.State!.Entries.First(e => e.TokenId == "b").Id;
        Tokens.First(t => t.Id == "b").ConditionList.Single().Duration.Should().Be(new ConditionDuration(bruno, 3));
    }

    [Fact]
    public async Task Condition_chip_shows_rounds_left()
    {
        var cut = await RenderAsync();
        await cut.InvokeAsync(() => Combat.AddCondition("a", new ConditionDraft("frightened", "Asustado", null, 3, Combat.State!.Entries[0].Id)));
        cut.Render();

        cut.Find("[data-testid=strip-row] [data-testid=condition-chip]").TextContent.Should().Contain("Asustado").And.Contain("3");
    }

    [Fact]
    public async Task Expired_condition_chip_has_expired_style()
    {
        var cut = await RenderAsync();
        var entry = Combat.State!.Entries[0].Id;
        await cut.InvokeAsync(() => Combat.AddCondition("a", new ConditionDraft("frightened", "Asustado", null, 1, entry)));
        await cut.InvokeAsync(() => Combat.NextTurn());
        await cut.InvokeAsync(() => Combat.NextTurn());
        cut.Render();

        cut.Find("[data-testid=condition-chip]").ClassList.Should().Contain("combat-condition--expired");
    }

    [Fact]
    public async Task Removing_a_chip_removes_the_condition()
    {
        var cut = await RenderAsync();
        await cut.InvokeAsync(() => Combat.AddCondition("a", new ConditionDraft(null, "Bendecido", null, null, null)));
        cut.Render();

        await cut.InvokeAsync(() => cut.Find("[data-testid=condition-chip] button").Click());

        Ana.ConditionList.Should().BeEmpty();
    }

    // --- Edición de un estado existente ---

    private Task EditChip(IRenderedComponent<DmShell> cut) =>
        cut.Find("[data-testid=strip-row] [data-testid=condition-edit]").ClickAsync();

    [Fact]
    public async Task Edit_raises_the_level_of_an_existing_condition()
    {
        var cut = await RenderAsync();
        await cut.InvokeAsync(() => Combat.AddCondition("a", new ConditionDraft("exhaustion", "Agotamiento", 2, null, null)));
        cut.Render();

        await EditChip(cut);
        await cut.InvokeAsync(() => cut.Find("[data-testid=condition-level] input").Change("3"));
        await cut.InvokeAsync(() => cut.Find("[data-testid=condition-save]").Click());

        Ana.ConditionList.Single().Level.Should().Be(3);
    }

    [Fact]
    public async Task Edit_is_preloaded_with_the_current_level()
    {
        var cut = await RenderAsync();
        await cut.InvokeAsync(() => Combat.AddCondition("a", new ConditionDraft("exhaustion", "Agotamiento", 2, null, null)));
        cut.Render();

        await EditChip(cut);

        cut.Find("[data-testid=condition-level] input").GetAttribute("value").Should().Be("2");
    }

    [Fact]
    public async Task Edit_changes_the_rounds_and_keeps_the_anchor()
    {
        var cut = await RenderAsync();
        var bruno = Combat.State!.Entries.First(e => e.TokenId == "b").Id;
        await cut.InvokeAsync(() => Combat.AddCondition("a", new ConditionDraft("frightened", "Asustado", null, 3, bruno)));
        cut.Render();

        await EditChip(cut);
        await cut.InvokeAsync(() => cut.Find("[data-testid=condition-rounds]").Input("5"));
        await cut.InvokeAsync(() => cut.Find("[data-testid=condition-save]").Click());

        Ana.ConditionList.Single().Duration.Should().Be(new ConditionDuration(bruno, 5));
    }

    [Fact]
    public async Task Edit_with_new_rounds_clears_the_expired_mark()
    {
        var cut = await RenderAsync();
        await cut.InvokeAsync(() => Combat.AddCondition("a", new ConditionDraft("frightened", "Asustado", null, 1, Combat.State!.Entries[0].Id)));
        await cut.InvokeAsync(() => Combat.NextTurn());
        await cut.InvokeAsync(() => Combat.NextTurn());
        cut.Render();

        await EditChip(cut);
        await cut.InvokeAsync(() => cut.Find("[data-testid=condition-rounds]").Input("2"));
        await cut.InvokeAsync(() => cut.Find("[data-testid=condition-save]").Click());

        Ana.ConditionList.Single().Expired.Should().BeFalse();
    }

    [Fact]
    public async Task Edit_does_not_add_a_second_condition()
    {
        var cut = await RenderAsync();
        await cut.InvokeAsync(() => Combat.AddCondition("a", new ConditionDraft("frightened", "Asustado", null, 3, Combat.State!.Entries[0].Id)));
        cut.Render();

        await EditChip(cut);
        await cut.InvokeAsync(() => cut.Find("[data-testid=condition-save]").Click());

        Ana.ConditionList.Should().HaveCount(1);
    }

    [Fact]
    public async Task Edit_is_not_offered_when_read_only()
    {
        var cut = await RenderAsync();
        await cut.InvokeAsync(() => Combat.AddCondition("a", new ConditionDraft("frightened", "Asustado", null, 3, Combat.State!.Entries[0].Id)));
        MakeReadOnly();
        cut = Render<DmShell>();

        cut.FindAll("[data-testid=condition-edit]").Should().BeEmpty();
    }

    [Fact]
    public async Task Editor_is_not_a_form_so_enter_does_not_submit()
    {
        var cut = await RenderAsync();
        await cut.InvokeAsync(() => cut.Find("[data-testid=add-condition]").Click());

        cut.FindAll("form[data-testid=condition-editor]").Should().BeEmpty();
    }

    private static async Task SelectByLabel(IRenderedComponent<DmShell> cut, string label)
    {
        var options = cut.Find("select[data-testid=condition-select]").QuerySelectorAll("option").Where(o => !o.HasAttribute("hidden")).ToList();
        await cut.InvokeAsync(() => cut.Find("select[data-testid=condition-select]").Change(options.FindIndex(o => o.TextContent == label)));
    }
}
