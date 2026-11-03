using DmCompanion.UI.Components;
using DmCompanion.UI.Tests.Support;

namespace DmCompanion.UI.Tests;

public class IconTests : UiTestContext
{
    public static TheoryData<IconName> AllIcons() => new(Enum.GetValues<IconName>());

    [Theory]
    [MemberData(nameof(AllIcons))]
    public void Every_icon_name_has_its_svg_packaged(IconName name) =>
        File.Exists(UiAssets.PathOf("icons/" + IconFiles.FileName(name))).Should().BeTrue();

    [Fact]
    public void File_name_is_the_kebab_case_of_the_icon_name() =>
        IconFiles.FileName(IconName.ScrollText).Should().Be("scroll-text.svg");

    [Fact]
    public void Icon_points_its_mask_at_the_packaged_svg()
    {
        var cut = Render<Icon>(p => p.Add(x => x.Name, IconName.Swords));
        cut.Find(".ds-icon").GetAttribute("style").Should().Contain("_content/DmCompanion.UI/icons/swords.svg");
    }

    [Fact]
    public void Icon_is_hidden_from_assistive_technology()
    {
        var cut = Render<Icon>(p => p.Add(x => x.Name, IconName.X));
        cut.Find(".ds-icon").GetAttribute("aria-hidden").Should().Be("true");
    }
}
