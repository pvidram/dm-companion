namespace DmCompanion.Core.Maps;

/// <summary>Cambia solo el campo `name` del frontmatter de una nota de mapa; el resto del texto queda byte a byte como estaba.</summary>
public static class MapNoteName
{
    public static string WithName(string content, string name)
    {
        // Un BOM al principio (Windows, algunos editores) esconde el «---» de apertura: se aparta y se devuelve intacto.
        var bom = content.StartsWith('\uFEFF') ? "\uFEFF" : string.Empty;
        var lines = content[bom.Length..].Split('\n').ToList();
        if (lines.Count == 0 || lines[0].TrimEnd() != "---") return content;

        var close = lines.FindIndex(1, line => line.TrimEnd() == "---");
        if (close < 0) return content;

        var nameIndex = lines.FindIndex(1, close - 1, line => line.StartsWith("name:") || line.StartsWith("name :"));
        if (nameIndex >= 0)
        {
            // Un valor que sigue en líneas con sangría (YAML plegado) se sustituye entero.
            var end = nameIndex + 1;
            while (end < close && lines[end].Length > 0 && char.IsWhiteSpace(lines[end][0])) end++;
            var carriage = lines[nameIndex].EndsWith('\r') ? "\r" : string.Empty;
            lines.RemoveRange(nameIndex, end - nameIndex);
            lines.Insert(nameIndex, $"name: {Quote(name)}{carriage}");
        }
        else
        {
            var carriage = lines[0].EndsWith('\r') ? "\r" : string.Empty;
            lines.Insert(1, $"name: {Quote(name)}{carriage}");
        }
        return bom + string.Join('\n', lines);
    }

    private static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
