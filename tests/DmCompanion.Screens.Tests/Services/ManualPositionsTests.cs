using DmCompanion.Screens.Services;

namespace DmCompanion.Screens.Tests.Services;

public sealed class ManualPositionsTests
{
    [Fact]
    public void Remembers_per_manual()
    {
        var positions = new ManualPositions(new InMemoryAppPreferences());

        positions.Remember("manuales/dnd5e/A.pdf", 12);
        positions.Remember("manuales/dnd5e/B.pdf", 3);

        (positions.LastPage("manuales/dnd5e/A.pdf"), positions.LastPage("manuales/dnd5e/B.pdf"), positions.LastPage("manuales/dnd5e/C.pdf"))
            .Should().Be((12, 3, (int?)null));
    }
}
