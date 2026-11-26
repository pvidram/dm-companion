using AwesomeAssertions;
using DmCompanion.Screens.Services;
using DmCompanion.UI.Interop;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Screens.Tests.Services;

public class PanelLayoutStoreTests
{
    private readonly InMemoryAppPreferences _prefs = new();

    private PanelLayoutStore Create(ILogger<PanelLayoutStore>? logger = null) =>
        new(_prefs, logger ?? NullLogger<PanelLayoutStore>.Instance);

    [Fact]
    public void Size_round_trips()
    {
        var store = Create();
        store.SetSize(LayoutKeys.Notes, new WindowSize(420, 610));
        Create().GetSize(LayoutKeys.Notes).Should().Be(new WindowSize(420, 610));
    }

    [Fact]
    public void Setting_one_axis_keeps_the_other()
    {
        var store = Create();
        store.SetSize(LayoutKeys.Notes, new WindowSize(420, 610));
        store.SetSize(LayoutKeys.Notes, new WindowSize(500, null));
        store.GetSize(LayoutKeys.Notes).Should().Be(new WindowSize(500, 610));
    }

    [Fact]
    public void A_size_with_only_a_width_has_no_height()
    {
        var store = Create();
        store.SetSize(LayoutKeys.Notes, new WindowSize(500, null));
        Create().GetSize(LayoutKeys.Notes).Should().Be(new WindowSize(500, null));
    }

    [Fact]
    public void A_size_without_axes_stores_nothing()
    {
        Create().SetSize(LayoutKeys.Notes, new WindowSize(null, null));
        _prefs.Get("layout.panel.notes").Should().BeNull();
    }

    [Fact]
    public void A_non_positive_axis_is_not_stored()
    {
        var store = Create();
        store.SetSize(LayoutKeys.Notes, new WindowSize(500, -3));
        store.GetSize(LayoutKeys.Notes).Should().Be(new WindowSize(500, null));
    }

    [Fact]
    public void Reset_size_keeps_the_pin_and_anchor()
    {
        var store = Create();
        var anchor = new WindowAnchor(WindowCorner.BottomRight, 12, 34);
        store.SetSize(LayoutKeys.History, new WindowSize(300, 200));
        store.SetPinned(LayoutKeys.History, true);
        store.SetAnchor(LayoutKeys.History, anchor);
        store.ResetSize(LayoutKeys.History);
        (store.GetSize(LayoutKeys.History), store.IsPinned(LayoutKeys.History), store.GetAnchor(LayoutKeys.History))
            .Should().Be((null, true, anchor));
    }

    [Fact]
    public void Pinned_defaults_to_false() => Create().IsPinned(LayoutKeys.Maps).Should().BeFalse();

    [Fact]
    public void Anchor_round_trips()
    {
        var anchor = new WindowAnchor(WindowCorner.TopLeft, 5.5, 7);
        Create().SetAnchor(LayoutKeys.TokenCard, anchor);
        Create().GetAnchor(LayoutKeys.TokenCard).Should().Be(anchor);
    }

    [Fact]
    public void Keys_do_not_interfere()
    {
        var store = Create();
        store.SetSize(LayoutKeys.Notes, new WindowSize(100, 100));
        store.GetSize(LayoutKeys.Players).Should().BeNull();
    }

    [Fact]
    public void Invalid_json_is_ignored()
    {
        _prefs.Set("layout.panel.notes", "{no");
        Create().GetSize(LayoutKeys.Notes).Should().BeNull();
    }

    [Fact]
    public void Invalid_json_logs_a_warning()
    {
        _prefs.Set("layout.panel.notes", "{no");
        var logger = new CapturingLogger();
        Create(logger).GetSize(LayoutKeys.Notes);
        logger.Levels.Should().Contain(LogLevel.Warning);
    }

    [Fact]
    public void Invalid_json_logs_the_warning_once_per_key()
    {
        _prefs.Set("layout.panel.notes", "{no");
        var logger = new CapturingLogger();
        var store = Create(logger);
        store.GetSize(LayoutKeys.Notes);
        store.GetSize(LayoutKeys.Notes);
        store.IsPinned(LayoutKeys.Notes);
        logger.Levels.Should().ContainSingle();
    }

    [Fact]
    public void Non_positive_size_is_ignored()
    {
        _prefs.Set("layout.panel.notes", """{"w":-5,"h":0}""");
        Create().GetSize(LayoutKeys.Notes).Should().BeNull();
    }

    [Fact]
    public void Unknown_corner_is_ignored()
    {
        _prefs.Set("layout.window.history", """{"anchor":{"corner":"zz","dx":1,"dy":2}}""");
        Create().GetAnchor(LayoutKeys.History).Should().BeNull();
    }

    [Fact]
    public void Empty_entry_removes_the_key()
    {
        var store = Create();
        store.SetPinned(LayoutKeys.Sound, true);
        store.SetPinned(LayoutKeys.Sound, false);
        _prefs.Get("layout.panel.sound").Should().BeNull();
    }

    private sealed class CapturingLogger : ILogger<PanelLayoutStore>
    {
        public List<LogLevel> Levels { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Levels.Add(logLevel);
    }
}
