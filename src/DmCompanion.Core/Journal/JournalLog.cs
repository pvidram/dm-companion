using System.Globalization;
using System.Text;
using DmCompanion.Core.Vault;

namespace DmCompanion.Core.Journal;

public enum JournalLogDecision { Create, Overwrite, ForeignNote }

public static class JournalLog
{
    public static IReadOnlyList<JournalEntry> Order(IEnumerable<JournalEntry> entries) =>
        [.. entries.OrderBy(entry => entry.Added.UtcDateTime).ThenBy(entry => entry.Path, StringComparer.Ordinal)];

    public static string Render(IEnumerable<JournalEntry> entries, DateTimeOffset generatedAt)
    {
        var text = new StringBuilder("---\ntype: journal-log\ngenerated: true\n---\n");
        text.Append("Generado por DM Companion el ").Append(generatedAt.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture))
            .Append(". Se sobrescribe al regenerar\n");
        foreach (var entry in Order(entries))
        {
            text.Append("\n## ").Append(entry.OwnerName).Append(" · ").Append(entry.Title).Append(" · ")
                .Append(entry.Added.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)).Append("\n\n");
            var images = entry.Images.Where(image => image.Exists).ToList();
            foreach (var image in images) text.Append("![[").Append(image.Path).Append("]]\n");
            if (entry.Note.Length > 0)
            {
                if (images.Count > 0) text.Append('\n');
                text.Append(entry.Note).Append('\n');
            }
        }
        return text.ToString();
    }

    /// <summary>Solo se sobrescribe una nota con <c>generated: true</c>; cualquier otra existente es del DM y no se toca.</summary>
    public static JournalLogDecision Decide(string? existingContent)
    {
        if (existingContent is null) return JournalLogDecision.Create;
        var fields = FrontmatterParser.Parse(existingContent).Fields;
        return FrontmatterReader.GetBool(fields, "generated") ? JournalLogDecision.Overwrite : JournalLogDecision.ForeignNote;
    }
}
