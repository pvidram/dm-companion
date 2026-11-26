using DmCompanion.Screens.Services;

namespace DmCompanion.Screens.Tests;

public sealed class BackNavigationJournalTests
{
    private const string Entry = "Personajes/Lyra/Diario/2026-11-16 Ankheg.md";

    private readonly ShellState _shell = new();
    private BackNavigation Back => new(_shell);

    [Fact]
    public async Task Back_closes_a_journal_form_without_changes_and_nothing_else()
    {
        _shell.OpenJournalEntry(Entry, "Personajes/Lyra.md");
        _shell.OpenJournalForm(new JournalFormRequest(JournalFormMode.Edit, null, Entry));

        var handled = await Back.HandleBackAsync();

        (handled, _shell.JournalForm, _shell.ConfirmingJournalDiscard, _shell.SelectedJournalPath, _shell.ActivePanel).Should().Be((true, null, false, Entry, OverlayPanel.Players));
    }

    [Fact]
    public async Task Back_on_a_journal_form_with_unsaved_changes_asks_instead_of_closing()
    {
        _shell.OpenJournalForm(new JournalFormRequest(JournalFormMode.New));
        _shell.SetJournalFormDirtyProbe(() => true);

        var handled = await Back.HandleBackAsync();

        (handled, _shell.JournalForm is not null, _shell.ConfirmingJournalDiscard).Should().Be((true, true, true));
    }

    [Fact]
    public async Task Back_with_the_discard_question_open_keeps_editing()
    {
        _shell.OpenJournalForm(new JournalFormRequest(JournalFormMode.New));
        _shell.SetJournalFormDirtyProbe(() => true);
        await Back.HandleBackAsync();

        var handled = await Back.HandleBackAsync();

        (handled, _shell.JournalForm is not null, _shell.ConfirmingJournalDiscard).Should().Be((true, true, false));
    }

    [Fact]
    public async Task Back_cancels_the_delete_confirmation_before_the_form_or_the_entry()
    {
        _shell.OpenJournalEntry(Entry, "Personajes/Lyra.md");
        _shell.RequestDeleteJournalEntry(Entry);

        var handled = await Back.HandleBackAsync();

        (handled, _shell.DeletingJournalPath, _shell.SelectedJournalPath).Should().Be((true, null, Entry));
    }

    [Fact]
    public async Task Back_from_an_open_entry_returns_to_the_diary_list_and_keeps_the_panel()
    {
        _shell.OpenJournalEntry(Entry, "Personajes/Lyra.md");

        var handled = await Back.HandleBackAsync();

        (handled, _shell.SelectedJournalPath, _shell.ActivePanel, _shell.PlayerTab).Should().Be((true, null, OverlayPanel.Players, PlayerTab.Journal));
    }

    [Fact]
    public async Task Back_from_the_diary_list_closes_the_panel()
    {
        _shell.OpenJournalEntry(Entry, "Personajes/Lyra.md");
        _shell.CloseJournalEntry();

        var handled = await Back.HandleBackAsync();

        (handled, _shell.ActivePanel).Should().Be((true, OverlayPanel.None));
    }

    [Fact]
    public async Task Back_with_an_entry_open_in_another_panel_does_not_touch_it()
    {
        _shell.OpenJournalEntry(Entry, "Personajes/Lyra.md");
        _shell.Toggle(OverlayPanel.Notes);

        var handled = await Back.HandleBackAsync();

        (handled, _shell.SelectedJournalPath, _shell.ActivePanel).Should().Be((true, Entry, OverlayPanel.None));
    }
}
