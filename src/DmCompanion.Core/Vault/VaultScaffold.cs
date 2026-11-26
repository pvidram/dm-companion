using System.Text.RegularExpressions;
using DmCompanion.Core.GameSystems;

namespace DmCompanion.Core.Vault;

/// <summary>Contenido de un vault nuevo: la nota de campaña en la raíz y un `.obsidian/app.json` vacío.</summary>
public sealed record VaultScaffold(string NoteRelativePath, string NoteContent, string AppJson)
{
    public static VaultScaffold? Plan(string campaignName, IGameSystem system)
    {
        var name = Regex.Replace(
            new string((campaignName ?? string.Empty).Select(c => char.IsControl(c) ? ' ' : c).ToArray()),
            " {2,}", " ").Trim();
        if (name.Length == 0 || !NoteFileName.TryFromTitle(name, out var fileName)) return null;
        return new VaultScaffold(
            fileName + ".md",
            NoteTemplates.Create(EntityType.Campaign, name, system),
            "{}");
    }
}
