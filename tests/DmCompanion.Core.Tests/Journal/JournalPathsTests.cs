using DmCompanion.Core.Journal;

namespace DmCompanion.Core.Tests.Journal;

public class JournalPathsTests
{
    [Theory]
    [InlineData("Personajes/Lyra.md", "Personajes/Lyra/Diario")]
    [InlineData("Personajes/Lyra/Lyra.md", "Personajes/Lyra/Diario")]
    [InlineData("personajes/lyra/LYRA.md", "personajes/lyra/Diario")]
    [InlineData("Lyra.md", "Lyra/Diario")]
    [InlineData("Personajes/Lyra/Ficha.md", "Personajes/Lyra/Ficha/Diario")]
    public void Diary_folder_follows_the_own_folder_rule(string pc, string expected) =>
        JournalPaths.DiaryFolderOf(pc).Should().Be(expected);

    [Theory]
    [InlineData("Personajes/Lyra/Diario/2026-11-16 Ankheg.md", true)]
    [InlineData("Personajes/Lyra/diario/2026-11-16 Ankheg.md", true)]
    [InlineData("Personajes/Lyra/Diario/Sub/2026-11-16 Ankheg.md", false)]
    [InlineData("Personajes/Lyra/Diario/2026-11-16 Ankheg.png", false)]
    [InlineData("Personajes/Lyra/Diario/2026-11-16 Ankheg.sync-conflict-20261116-101500-ABCDEF1.md", false)]
    [InlineData("Personajes/Lyra/Notas/2026-11-16 Ankheg.md", false)]
    public void Entry_path_is_a_markdown_note_directly_in_a_Diario_folder(string path, bool expected) =>
        JournalPaths.IsEntryPath(path).Should().Be(expected);

    [Theory]
    [InlineData("Personajes/Lyra/Diario/a.JPG", true)]
    [InlineData("Personajes/Lyra/Diario/a.heic", true)]
    [InlineData("Personajes/Lyra/Diario/a.bmp", false)]
    [InlineData("Mapas/a.png", false)]
    public void Diary_image_is_a_supported_image_in_a_Diario_folder(string path, bool expected) =>
        JournalPaths.IsDiaryImage(path).Should().Be(expected);

    [Theory]
    [InlineData("a.jpg", true)]
    [InlineData("a.heic", false)]
    public void Heic_is_stored_but_not_displayable(string name, bool expected) =>
        JournalPaths.IsDisplayable(name).Should().Be(expected);

    [Fact]
    public void Owner_is_the_folder_before_Diario() =>
        JournalPaths.OwnerNameOf("Personajes/Lyra/Diario/2026-11-16 Ankheg.md").Should().Be("Lyra");
}
