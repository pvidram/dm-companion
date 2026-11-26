using AwesomeAssertions;
using DmCompanion.Core.Vault;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Services;
using DmCompanion.UI.Interop;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Screens.Tests.Services;

public sealed class WindowManagerTests : IAsyncLifetime
{
    private const string MapNote = "Mapas/cripta.md";
    private const string OtherNote = "Mapas/torre.md";

    private readonly TempVault _vault = TempVault.Empty();
    private readonly InMemoryAppPreferences _prefs = new();
    private readonly VaultHost _host;
    private readonly MapSession _session;
    private readonly ShellState _shell = new();
    private readonly PanelLayoutStore _layout;
    private readonly WindowManager _windows;

    public WindowManagerTests()
    {
        _host = new VaultHost(new FileSystemVaultServiceFactory(NullLoggerFactory.Instance), _prefs, NullLogger<VaultHost>.Instance);
        _session = new MapSession(_host, _prefs, TimeProvider.System, NullLogger<MapSession>.Instance);
        _layout = new PanelLayoutStore(_prefs, NullLogger<PanelLayoutStore>.Instance);
        _windows = new WindowManager(_shell, _session, _layout);
    }

    public async Task InitializeAsync()
    {
        WriteNote(MapNote, "Cripta");
        WriteNote(OtherNote, "Torre");
        await _host.OpenAsync(_vault.Root);
    }

    public async Task DisposeAsync()
    {
        _windows.Dispose();
        await _session.DisposeAsync();
        await _host.DisposeAsync();
        _vault.Dispose();
    }

    private string[] Ids => [.. _windows.TokenCards.Select(c => c.TokenId)];

    [Fact]
    public void Opening_two_creatures_replaces_the_unpinned_card()
    {
        _windows.OpenTokenCard("a", 1, 1);
        _windows.OpenTokenCard("b", 2, 2);
        Ids.Should().Equal("b");
    }

    [Fact]
    public void Pinned_cards_stay_when_another_opens()
    {
        _windows.OpenTokenCard("a", 1, 1);
        _windows.SetPinned("a", true);
        _windows.OpenTokenCard("b", 2, 2);
        Ids.Should().Equal("a", "b");
    }

    [Fact]
    public void Opening_an_open_card_brings_it_to_front_without_duplicating()
    {
        _windows.OpenTokenCard("a", 1, 1);
        _windows.SetPinned("a", true);
        _windows.OpenTokenCard("b", 2, 2);
        _windows.OpenTokenCard("a", 50, 50);
        (string.Join(",", Ids), _windows.TokenCards[^1].X).Should().Be(("b,a", 1d));
    }

    [Fact]
    public void A_card_being_edited_is_not_replaced()
    {
        _windows.OpenTokenCard("a", 1, 1);
        _windows.SetEditing("a", true);
        _windows.OpenTokenCard("b", 2, 2);
        Ids.Should().Equal("a", "b");
    }

    [Fact]
    public void After_editing_the_oldest_unpinned_card_is_replaced()
    {
        _windows.OpenTokenCard("a", 1, 1);
        _windows.SetEditing("a", true);
        _windows.OpenTokenCard("b", 2, 2);
        _windows.SetEditing("a", false);
        _windows.OpenTokenCard("c", 3, 3);
        Ids.Should().Equal("b", "c");
    }

    [Fact]
    public void New_cards_use_the_last_chosen_size()
    {
        _windows.OpenTokenCard("a", 1, 1);
        _windows.UpdateFrame("a", new WindowFrameChange(1, 1, new WindowSize(400, 500), null));
        _windows.OpenTokenCard("b", 2, 2);
        _windows.TokenCards.Single().Size.Should().Be(new WindowSize(400, 500));
    }

    [Fact]
    public void Moving_a_card_does_not_store_a_size()
    {
        _windows.OpenTokenCard("a", 1, 1);
        _windows.UpdateFrame("a", new WindowFrameChange(30, 40, null, null));
        (_windows.TokenCards.Single().X, _layout.GetSize(LayoutKeys.TokenCard)).Should().Be((30, null));
    }

    [Fact]
    public void Resizing_only_the_width_keeps_the_stored_height()
    {
        _windows.OpenTokenCard("a", 1, 1);
        _windows.UpdateFrame("a", new WindowFrameChange(1, 1, new WindowSize(400, 500), null));
        _windows.UpdateFrame("a", new WindowFrameChange(1, 1, new WindowSize(600, null), null));
        (_windows.TokenCards.Single().Size, _layout.GetSize(LayoutKeys.TokenCard)).Should().Be((new WindowSize(600, 500), new WindowSize(600, 500)));
    }

    [Fact]
    public void Resizing_one_card_does_not_resize_the_others()
    {
        _windows.OpenTokenCard("a", 1, 1);
        _windows.SetPinned("a", true);
        _windows.OpenTokenCard("b", 2, 2);
        _windows.UpdateFrame("b", new WindowFrameChange(2, 2, new WindowSize(400, 500), null));
        _windows.TokenCards.Single(c => c.TokenId == "a").Size.Should().BeNull();
    }

    [Fact]
    public void Reset_size_clears_the_stored_default()
    {
        _windows.OpenTokenCard("a", 1, 1);
        _windows.UpdateFrame("a", new WindowFrameChange(1, 1, new WindowSize(400, 500), null));
        _windows.ResetSize("a");
        (_windows.TokenCards.Single().Size, _layout.GetSize(LayoutKeys.TokenCard)).Should().Be((null, null));
    }

    [Fact]
    public void Opening_a_card_closes_an_unpinned_history()
    {
        _shell.ToggleHistory();
        _windows.OpenTokenCard("a", 1, 1);
        _shell.HistoryOpen.Should().BeFalse();
    }

    [Fact]
    public void Opening_a_card_keeps_a_pinned_history()
    {
        _shell.ToggleHistory();
        _layout.SetPinned(LayoutKeys.History, true);
        _windows.OpenTokenCard("a", 1, 1);
        _shell.HistoryOpen.Should().BeTrue();
    }

    [Fact]
    public async Task Changing_map_closes_every_card()
    {
        await _session.OpenAsync(MapNote);
        _windows.OpenTokenCard("a", 1, 1);
        _windows.SetPinned("a", true);
        _windows.OpenTokenCard("b", 2, 2);
        await _session.OpenAsync(OtherNote);
        Ids.Should().BeEmpty();
    }

    [Fact]
    public async Task Closing_the_map_closes_every_card()
    {
        await _session.OpenAsync(MapNote);
        _windows.OpenTokenCard("a", 1, 1);
        await _session.CloseAsync();
        Ids.Should().BeEmpty();
    }

    [Fact]
    public async Task Resizing_the_open_map_image_keeps_the_cards()
    {
        await _session.OpenAsync(MapNote);
        _windows.OpenTokenCard("a", 1, 1);
        _session.SetImageSize(MapNote, 800, 600);
        Ids.Should().Equal("a");
    }

    [Fact]
    public void Closing_a_pinned_card_removes_it()
    {
        _windows.OpenTokenCard("a", 1, 1);
        _windows.SetPinned("a", true);
        _windows.Close("a");
        Ids.Should().BeEmpty();
    }

    [Fact]
    public void Changed_is_not_raised_for_an_unknown_card()
    {
        var raised = 0;
        _windows.Changed += () => raised++;
        _windows.Close("zzz");
        raised.Should().Be(0);
    }

    private void WriteNote(string path, string name)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_vault.PathOf(path))!);
        File.WriteAllText(_vault.PathOf(path), $"---\ntype: map\nname: {name}\n---\n");
    }
}
