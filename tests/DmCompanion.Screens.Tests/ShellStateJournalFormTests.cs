using DmCompanion.Screens.Services;

namespace DmCompanion.Screens.Tests;

public sealed class ShellStateJournalFormTests
{
    [Fact]
    public void OpenJournalForm_stores_the_request_and_raises_Changed()
    {
        var shell = new ShellState();
        var raised = 0;
        shell.Changed += (_, _) => raised++;

        shell.OpenJournalForm(new JournalFormRequest(JournalFormMode.New, "Personajes/Lyra.md"));

        (shell.JournalForm, raised).Should().Be((new JournalFormRequest(JournalFormMode.New, "Personajes/Lyra.md"), 1));
    }

    [Fact]
    public void CloseJournalForm_clears_it()
    {
        var shell = new ShellState();
        shell.OpenJournalForm(new JournalFormRequest(JournalFormMode.Shared));

        shell.CloseJournalForm();

        shell.JournalForm.Should().BeNull();
    }

    [Fact]
    public void CloseJournalForm_with_nothing_open_raises_nothing()
    {
        var shell = new ShellState();
        var raised = 0;
        shell.Changed += (_, _) => raised++;

        shell.CloseJournalForm();

        raised.Should().Be(0);
    }

    [Fact]
    public void Delete_request_and_cancel()
    {
        var shell = new ShellState();
        shell.RequestDeleteJournalEntry("Personajes/Lyra/Diario/2026-11-16 Ankheg.md");
        var asked = shell.DeletingJournalPath;

        shell.CancelDeleteJournalEntry();

        (asked, shell.DeletingJournalPath).Should().Be(("Personajes/Lyra/Diario/2026-11-16 Ankheg.md", null));
    }

    [Fact]
    public void SetJournalNotice_stores_and_clears_the_text()
    {
        var shell = new ShellState();
        shell.SetJournalNotice("No se pudo leer 2 imagen(es)");
        var text = shell.JournalNotice;

        shell.SetJournalNotice(null);

        (text, shell.JournalNotice).Should().Be(("No se pudo leer 2 imagen(es)", null));
    }

    [Fact]
    public void Reset_closes_the_form_the_delete_and_the_notice()
    {
        var shell = new ShellState();
        shell.OpenJournalForm(new JournalFormRequest(JournalFormMode.New));
        shell.RequestDeleteJournalEntry("x.md");
        shell.SetJournalNotice("aviso");

        shell.Reset();

        (shell.JournalForm, shell.DeletingJournalPath, shell.JournalNotice).Should().Be((null, null, null));
    }

    [Fact]
    public void Closing_a_form_without_changes_closes_it()
    {
        var shell = new ShellState();
        shell.OpenJournalForm(new JournalFormRequest(JournalFormMode.New));
        shell.SetJournalFormDirtyProbe(() => false);

        shell.RequestCloseJournalForm();

        (shell.JournalForm, shell.ConfirmingJournalDiscard).Should().Be((null, false));
    }

    [Fact]
    public void Closing_a_form_with_changes_asks_and_closing_for_real_clears_the_question()
    {
        var shell = new ShellState();
        shell.OpenJournalForm(new JournalFormRequest(JournalFormMode.New));
        shell.SetJournalFormDirtyProbe(() => true);
        shell.RequestCloseJournalForm();
        var asked = (shell.JournalForm is not null, shell.ConfirmingJournalDiscard);

        shell.CloseJournalForm();

        (asked, shell.ConfirmingJournalDiscard).Should().Be(((true, true), false));
    }
}
