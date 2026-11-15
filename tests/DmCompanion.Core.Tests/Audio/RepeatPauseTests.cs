using DmCompanion.Core.Audio;

namespace DmCompanion.Core.Tests.Audio;

public class RepeatPauseTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(42)]
    public void RepeatPause_stays_in_range(int seed)
    {
        var random = new Random(seed);
        var repeat = EffectRepeat.WithPause(3, 7);

        var pauses = Enumerable.Range(0, 200).Select(_ => RepeatPause.Next(repeat, random).TotalSeconds);

        pauses.Should().OnlyContain(s => s >= 3 && s <= 7 && s == Math.Floor(s));
    }

    [Fact]
    public void Fixed_pause_when_min_equals_max() =>
        RepeatPause.Next(EffectRepeat.WithPause(5, 5), new Random(1)).Should().Be(TimeSpan.FromSeconds(5));
}
