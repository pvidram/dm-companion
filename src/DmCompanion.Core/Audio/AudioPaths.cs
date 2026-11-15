using DmCompanion.Core.Vault;

namespace DmCompanion.Core.Audio;

public static class AudioPaths
{
    public const string Root = "_audio";
    public const string AppFolder = "_audio/_dm";

    private const string RootPrefix = Root + "/";

    public static readonly IReadOnlyList<string> Extensions = [".mp3", ".ogg", ".wav", ".m4a", ".flac"];

    public static bool IsAudio(string rel) =>
        Extensions.Contains(Path.GetExtension(rel), StringComparer.OrdinalIgnoreCase);

    public static bool IsInLibrary(string rel)
    {
        var path = VaultPaths.Normalize(rel);
        if (!path.StartsWith(RootPrefix, StringComparison.OrdinalIgnoreCase) || !IsAudio(path)) return false;
        var segments = path[RootPrefix.Length..].Split('/');
        return segments.All(segment => segment.Length > 0 && segment[0] is not ('_' or '.'))
            && !VaultPaths.IsConflictFile(path);
    }

    public static string ContentTypeOf(string rel) => Path.GetExtension(rel).ToLowerInvariant() switch
    {
        ".mp3" => "audio/mpeg",
        ".ogg" => "audio/ogg",
        ".wav" => "audio/wav",
        ".m4a" => "audio/mp4",
        ".flac" => "audio/flac",
        _ => "application/octet-stream",
    };

    public static string ToLibraryPath(string vaultRel)
    {
        var path = VaultPaths.Normalize(vaultRel);
        return path.StartsWith(RootPrefix, StringComparison.OrdinalIgnoreCase) ? path[RootPrefix.Length..] : path;
    }

    public static string ToVaultPath(string libraryRel) => RootPrefix + VaultPaths.Normalize(libraryRel);
}
