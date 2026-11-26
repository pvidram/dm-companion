using DmCompanion.Screens.Services;

namespace DmCompanion.Screens.Tests;

public sealed class ShellStateTests
{
    [Fact]
    public void RequestNewNote_stores_the_request_and_raises_Changed()
    {
        var shell = new ShellState();
        var raised = 0;
        shell.Changed += (_, _) => raised++;

        shell.RequestNewNote("Mazmorra", "Lugares");

        (shell.NewNote, raised).Should().Be((new NewNoteRequest("Mazmorra", "Lugares"), 1));
    }

    [Fact]
    public void CloseNewNote_clears_the_request_and_raises_Changed()
    {
        var shell = new ShellState();
        shell.RequestNewNote("Mazmorra", null);
        var raised = 0;
        shell.Changed += (_, _) => raised++;

        shell.CloseNewNote();

        (shell.NewNote, raised).Should().Be((null, 1));
    }

    [Fact]
    public void Reset_clears_panel_selections_and_dialogs()
    {
        var shell = new ShellState();
        shell.EditNote("Personajes/Lyra.md");
        shell.SelectPc("Personajes/Torvald.md");
        shell.RequestNewNote("x", null);
        shell.OpenConflicts();
        var raised = 0;
        shell.Changed += (_, _) => raised++;

        shell.Reset();

        (shell.ActivePanel, shell.SelectedNotePath, shell.EditingPath, shell.SelectedPcPath, shell.NewNote, shell.ConflictsOpen, raised)
            .Should().Be((OverlayPanel.None, null, null, null, null, false, 1));
    }

    [Fact]
    public void OpenAddPlayer_sets_the_flag_and_CloseAddPlayer_clears_it()
    {
        var shell = new ShellState();
        var raised = 0;
        shell.Changed += (_, _) => raised++;

        shell.OpenAddPlayer();
        var opened = shell.AddPlayerOpen;
        shell.CloseAddPlayer();

        (opened, shell.AddPlayerOpen, raised).Should().Be((true, false, 2));
    }

    [Fact]
    public void Reset_closes_the_add_player_dialog()
    {
        var shell = new ShellState();
        shell.OpenAddPlayer();

        shell.Reset();

        shell.AddPlayerOpen.Should().BeFalse();
    }

    [Fact]
    public void Reset_closes_the_add_token_dialog()
    {
        var shell = new ShellState();
        shell.OpenAddToken();

        shell.Reset();

        shell.AddTokenOpen.Should().BeFalse();
    }

    [Fact]
    public void Reset_clears_the_add_token_prefill()
    {
        var shell = new ShellState();
        shell.OpenAddToken(new DmCompanion.Core.Maps.TokenDraft("Goblin", "srd:criaturas/goblin-warrior", DmCompanion.Core.Maps.TokenSide.Enemy, 1, 15, 10));

        shell.Reset();

        shell.AddTokenPrefill.Should().BeNull();
    }

    [Fact]
    public void RequestMapName_stores_the_request_and_raises_Changed()
    {
        var shell = new ShellState();
        var raised = 0;
        shell.Changed += (_, _) => raised++;

        shell.RequestMapName(MapNameMode.Rename, "Mapas/Cueva.md", "Cueva");

        (shell.MapName, raised).Should().Be((new MapNameRequest(MapNameMode.Rename, "Mapas/Cueva.md", "Cueva"), 1));
    }

    [Fact]
    public void CloseMapName_clears_the_request()
    {
        var shell = new ShellState();
        shell.RequestMapName(MapNameMode.NewBlank);

        shell.CloseMapName();

        shell.MapName.Should().BeNull();
    }

    [Fact]
    public void RequestDeleteMap_and_CancelDeleteMap_set_and_clear_the_path()
    {
        var shell = new ShellState();
        shell.RequestDeleteMap("Mapas/Cueva.md");
        var requested = shell.DeletingMapPath;

        shell.CancelDeleteMap();

        (requested, shell.DeletingMapPath).Should().Be(("Mapas/Cueva.md", null));
    }

    [Fact]
    public void ClosePanel_closes_only_the_panel_that_is_active()
    {
        var shell = new ShellState();
        shell.Toggle(OverlayPanel.Maps);

        shell.ClosePanel(OverlayPanel.Notes);

        shell.ActivePanel.Should().Be(OverlayPanel.Maps);
    }

    [Fact]
    public void Reset_clears_the_map_dialogs()
    {
        var shell = new ShellState();
        shell.RequestMapName(MapNameMode.NewBlank);
        shell.RequestDeleteMap("Mapas/Cueva.md");
        shell.RequestDiscardMap();

        shell.Reset();

        (shell.MapName, shell.DeletingMapPath, shell.DiscardingMap).Should().Be((null, null, false));
    }

    [Fact]
    public void OpenGlossary_sets_panel_and_key()
    {
        var shell = new ShellState();

        shell.OpenGlossary("srd:estados/blinded");

        (shell.ActivePanel, shell.SelectedGlossaryKey).Should().Be((OverlayPanel.Glossary, "srd:estados/blinded"));
    }

    [Fact]
    public void Reset_clears_the_glossary_selection()
    {
        var shell = new ShellState();
        shell.OpenGlossary("srd:estados/blinded");

        shell.Reset();

        shell.SelectedGlossaryKey.Should().BeNull();
    }

    [Fact]
    public void Reset_clears_manual_tab_open_manual_and_pane()
    {
        var shell = new ShellState();
        shell.OpenManual("manuales/dnd5e/PHB.pdf");
        shell.ToggleManualPane(ManualPane.Outline);

        shell.Reset();

        (shell.GlossaryTab, shell.OpenManualPath, shell.ActiveManualPane).Should().Be((GlossaryTab.Glossary, null, ManualPane.None));
    }

    [Fact]
    public void ToggleManualViewerExpanded_alternates_and_raises_Changed()
    {
        var shell = new ShellState();
        shell.OpenManual("manuales/dnd5e/PHB.pdf");
        var raised = 0;
        shell.Changed += (_, _) => raised++;

        shell.ToggleManualViewerExpanded();
        var first = shell.ManualViewerExpanded;
        shell.ToggleManualViewerExpanded();

        (first, shell.ManualViewerExpanded, raised).Should().Be((true, false, 2));
    }

    [Fact]
    public void CloseManual_shrinks_the_expanded_viewer()
    {
        var shell = new ShellState();
        shell.OpenManual("manuales/dnd5e/PHB.pdf");
        shell.ToggleManualViewerExpanded();

        shell.CloseManual();

        shell.ManualViewerExpanded.Should().BeFalse();
    }

    [Fact]
    public void Reset_shrinks_the_expanded_viewer()
    {
        var shell = new ShellState();
        shell.OpenManual("manuales/dnd5e/PHB.pdf");
        shell.ToggleManualViewerExpanded();

        shell.Reset();

        shell.ManualViewerExpanded.Should().BeFalse();
    }
}
