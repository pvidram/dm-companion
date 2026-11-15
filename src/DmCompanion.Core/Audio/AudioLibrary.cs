using System.Globalization;

namespace DmCompanion.Core.Audio;

public sealed record AudioTrack(string Path, string Name, string Section);

public sealed record AudioSection(string Path, string Name, IReadOnlyList<AudioTrack> Tracks);

public sealed class AudioLibrary
{
    private const CompareOptions LooseMatch = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;
    private static readonly CompareInfo Compare = CultureInfo.InvariantCulture.CompareInfo;

    public static AudioLibrary Empty { get; } = new([]);

    private AudioLibrary(IReadOnlyList<AudioTrack> tracks)
    {
        Tracks = tracks;
        Sections = [.. tracks
            .GroupBy(track => track.Section, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new AudioSection(group.Key, SectionName(group.Key), [.. group]))];
    }

    public IReadOnlyList<AudioSection> Sections { get; }

    public IReadOnlyList<AudioTrack> Tracks { get; }

    /// <summary>Recibe rutas relativas al vault; solo entran las que <see cref="AudioPaths.IsInLibrary"/> acepta.</summary>
    public static AudioLibrary Build(IEnumerable<string> vaultRelativePaths)
    {
        var tracks = vaultRelativePaths
            .Where(AudioPaths.IsInLibrary)
            .Select(AudioPaths.ToLibraryPath)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ThenBy(path => path, StringComparer.Ordinal)
            .Select(ToTrack)
            .ToList();
        return new AudioLibrary(tracks);
    }

    public IReadOnlyList<AudioTrack> Search(string query)
    {
        var text = query.Trim();
        return text.Length == 0 ? [] : [.. Tracks.Where(track => Contains(track.Path, text))];
    }

    public AudioTrack? FindTrack(string path)
    {
        var key = Key(path);
        return Tracks.FirstOrDefault(track => Same(track.Path, key));
    }

    public AudioSection? FindSection(string path)
    {
        var key = Key(path);
        return Sections.FirstOrDefault(section => Same(section.Path, key));
    }

    /// <summary>Acepta el nombre con o sin extensión. Candidatos del de ruta más corta al más largo.</summary>
    public IReadOnlyList<AudioTrack> FindByFileName(string fileName)
    {
        var name = fileName.Trim();
        if (name.Length == 0) return [];
        var withExtension = AudioPaths.IsAudio(name);
        return [.. Tracks
            .Where(track => Same(withExtension ? FileName(track.Path) : track.Name, name))
            .OrderBy(track => track.Path.Length)
            .ThenBy(track => track.Path, StringComparer.OrdinalIgnoreCase)];
    }

    private static AudioTrack ToTrack(string path)
    {
        var slash = path.LastIndexOf('/');
        return new AudioTrack(path, System.IO.Path.GetFileNameWithoutExtension(path), slash < 0 ? "" : path[..slash]);
    }

    private static string Key(string path) => path.Replace('\\', '/').Trim('/');

    private static string FileName(string path) => path[(path.LastIndexOf('/') + 1)..];

    private static string SectionName(string path) => path.Length == 0 ? "" : FileName(path);

    private static bool Same(string a, string b) => Compare.Compare(a, b, LooseMatch) == 0;

    private static bool Contains(string text, string query) => Compare.IndexOf(text, query, LooseMatch) >= 0;
}
