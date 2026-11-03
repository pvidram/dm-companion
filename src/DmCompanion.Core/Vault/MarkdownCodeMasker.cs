using System.Text.RegularExpressions;

namespace DmCompanion.Core.Vault;

internal static class MarkdownCodeMasker
{
    private static readonly Regex InlineCode = new(@"(?<!`)(`+)(?!`).+?(?<!`)\1(?!`)", RegexOptions.Compiled);

    public static string Mask(string text)
    {
        var chars = text.ToCharArray();
        string? openFence = null;
        var lineStart = 0;

        while (lineStart < text.Length)
        {
            var newline = text.IndexOf('\n', lineStart);
            var lineEnd = newline < 0 ? text.Length : newline;
            var line = text[lineStart..lineEnd];
            var trimmed = line.TrimStart(' ');
            var marker = line.Length - trimmed.Length <= 3 ? ReadFenceMarker(trimmed) : null;

            if (openFence is null && marker is not null && CanOpenFence(marker, trimmed))
            {
                openFence = marker;
                Blank(chars, lineStart, lineEnd);
            }
            else if (openFence is not null)
            {
                if (IsClosingFence(trimmed, marker, openFence)) openFence = null;
                Blank(chars, lineStart, lineEnd);
            }
            else
            {
                foreach (Match match in InlineCode.Matches(line))
                    Blank(chars, lineStart + match.Index, lineStart + match.Index + match.Length);
            }

            lineStart = lineEnd + 1;
        }

        return new string(chars);
    }

    // CommonMark: the info string of a backtick fence cannot contain a backtick.
    private static bool CanOpenFence(string marker, string trimmedLine) =>
        marker[0] != '`' || !trimmedLine.AsSpan(marker.Length).Contains('`');

    private static bool IsClosingFence(string trimmedLine, string? marker, string openFence) =>
        marker is not null
        && marker[0] == openFence[0]
        && marker.Length >= openFence.Length
        && trimmedLine.TrimEnd().Length == marker.Length;

    private static string? ReadFenceMarker(string trimmedLine)
    {
        if (trimmedLine.Length < 3 || (trimmedLine[0] != '`' && trimmedLine[0] != '~')) return null;
        var fenceChar = trimmedLine[0];
        var count = 0;
        while (count < trimmedLine.Length && trimmedLine[count] == fenceChar) count++;
        return count >= 3 ? new string(fenceChar, count) : null;
    }

    private static void Blank(char[] chars, int from, int to)
    {
        for (var i = from; i < to; i++)
            if (chars[i] != '\r') chars[i] = ' ';
    }
}
