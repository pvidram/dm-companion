using DmCompanion.Desktop.Window;

namespace DmCompanion.Desktop.Tests;

public sealed class WindowPlacementTests
{
    private static readonly ScreenArea[] OneScreen = [new(0, 0, 1920, 1080)];

    [Fact]
    public void Restore_returns_null_without_saved_bounds() =>
        WindowPlacement.Restore(null, OneScreen).Should().BeNull();

    [Fact]
    public void Restore_keeps_bounds_inside_a_screen() =>
        WindowPlacement.Restore(new WindowBounds(100, 80, 1200, 800, false), OneScreen)
            .Should().Be(new WindowBounds(100, 80, 1200, 800, false));

    [Fact]
    public void Restore_returns_null_when_no_screen_shows_enough_of_the_window() =>
        WindowPlacement.Restore(new WindowBounds(3000, 80, 1200, 800, false), OneScreen).Should().BeNull();

    [Fact]
    public void Restore_keeps_bounds_partly_visible_above_the_minimum() =>
        WindowPlacement.Restore(new WindowBounds(1800, 900, 1200, 800, false), OneScreen)
            .Should().Be(new WindowBounds(1800, 900, 1200, 800, false));

    [Fact]
    public void Restore_finds_the_window_on_a_second_screen()
    {
        ScreenArea[] screens = [new(0, 0, 1920, 1080), new(1920, 0, 2560, 1440)];

        WindowPlacement.Restore(new WindowBounds(2000, 100, 1200, 800, false), screens)
            .Should().Be(new WindowBounds(2000, 100, 1200, 800, false));
    }

    [Fact]
    public void Restore_raises_the_size_to_the_minimum() =>
        WindowPlacement.Restore(new WindowBounds(10, 10, 300, 200, false), OneScreen)
            .Should().Be(new WindowBounds(10, 10, WindowPlacement.MinWidth, WindowPlacement.MinHeight, false));

    [Fact]
    public void Restore_keeps_the_maximized_flag() =>
        WindowPlacement.Restore(new WindowBounds(0, 0, 1200, 800, true), OneScreen)!.Maximized.Should().BeTrue();

    [Fact]
    public void Restore_returns_null_without_screens() =>
        WindowPlacement.Restore(new WindowBounds(0, 0, 1200, 800, false), []).Should().BeNull();
}
