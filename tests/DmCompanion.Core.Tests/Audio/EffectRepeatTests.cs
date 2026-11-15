using DmCompanion.Core.Audio;

namespace DmCompanion.Core.Tests.Audio;

public class EffectRepeatTests
{
    [Theory]
    [InlineData(RepeatKind.RepeatWithPause, 0, 0, 1, 1)]
    [InlineData(RepeatKind.RepeatWithPause, 5, 2, 5, 5)]
    [InlineData(RepeatKind.RepeatWithPause, 3, 9, 3, 9)]
    [InlineData(RepeatKind.Once, 4, 8, 0, 0)]
    [InlineData(RepeatKind.Loop, 4, 8, 0, 0)]
    public void Normalize_fixes_the_pause_range(RepeatKind kind, int min, int max, int expectedMin, int expectedMax) =>
        new EffectRepeat(kind, min, max).Normalize().Should().Be(new EffectRepeat(kind, expectedMin, expectedMax));
}
