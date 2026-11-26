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

    [Theory]
    [InlineData(IconName.ScrollText, "scroll-text.svg")]
    [InlineData(IconName.Trash2, "trash-2.svg")]
    [InlineData(IconName.Undo2, "undo-2.svg")]
    [InlineData(IconName.ScanEye, "scan-eye.svg")]
    [InlineData(IconName.Delete, "delete.svg")]
    [InlineData(IconName.Grid3X3, "grid-3-x-3.svg")]
    public void File_name_is_the_kebab_case_of_the_icon_name(IconName name, string expected) =>
        IconFiles.FileName(name).Should().Be(expected);

    [Theory]
    [InlineData(IconName.Pin, "pin.svg")]
    [InlineData(IconName.PinOff, "pin-off.svg")]
    public void Pin_icons_map_to_their_svg_files(IconName name, string expected)
    {
        IconFiles.FileName(name).Should().Be(expected);
        File.Exists(UiAssets.PathOf("icons/" + expected)).Should().BeTrue();
    }

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
