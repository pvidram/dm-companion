using System.Globalization;
using DmCompanion.Core.Vault;

namespace DmCompanion.Core.Audio;

public enum AmbienceKind { None, Scene, Section, Track, NotFound }

/// <summary><c>Reference</c> es el nombre de la escena o la ruta (relativa a <c>_audio/</c>) de la sección o el track.</summary>
public sealed record AmbienceTarget(AmbienceKind Kind, string Label, string? Reference);

/// <summary>Resuelve el campo <c>ambience:</c> de una nota: escena, sección o track (en ese orden), o un enlace <c>[[x.mp3]]</c>.</summary>
public static class AmbienceResolver
{
    private const CompareOptions LooseMatch = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;
    private static readonly CompareInfo Compare = CultureInfo.InvariantCulture.CompareInfo;

    public static AmbienceTarget Resolve(string? value, IReadOnlyCollection<string> sceneNames, AudioLibrary library)
    {
        var text = value?.Trim() ?? "";
        if (text.Length == 0) return new AmbienceTarget(AmbienceKind.None, "", null);

        if (text.StartsWith("[[", StringComparison.Ordinal) && text.EndsWith("]]", StringComparison.Ordinal))
            return ResolveLink(text, library);

        // Los nombres de escena son nombres de fichero saneados: «Taberna: noche» cita a «Taberna noche».
        var sanitized = NoteFileName.FromTitle(text);
        if (sceneNames.FirstOrDefault(name => Compare.Compare(name, text, LooseMatch) == 0 || Compare.Compare(name, sanitized, LooseMatch) == 0) is { } scene)
            return new AmbienceTarget(AmbienceKind.Scene, scene, scene);
        if (library.FindSection(text) is { } section)
            return new AmbienceTarget(AmbienceKind.Section, section.Name, section.Path);
        if (library.FindTrack(text) is { } track)
            return new AmbienceTarget(AmbienceKind.Track, track.Name, track.Path);
        return new AmbienceTarget(AmbienceKind.NotFound, text, null);
    }

    private static AmbienceTarget ResolveLink(string text, AudioLibrary library)
    {
        var inner = text[2..^2];
        var cut = inner.IndexOfAny(['|', '#']);
        var target = (cut < 0 ? inner : inner[..cut]).Trim().Replace('\\', '/');
        var fileName = target[(target.LastIndexOf('/') + 1)..];
        return library.FindByFileName(fileName) is [var best, ..]
            ? new AmbienceTarget(AmbienceKind.Track, best.Name, best.Path)
            : new AmbienceTarget(AmbienceKind.NotFound, text, null);
    }
}
