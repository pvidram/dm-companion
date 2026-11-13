using DmCompanion.Core.Maps;

namespace DmCompanion.Screens.Tests;

/// <summary>Ficheros de un mapa de ejemplo dentro de un <see cref="TempVault"/>.</summary>
internal static class MapTestVault
{
    public const string MapNote = "Mapas/cripta.md";
    public const string Image = "Mapas/cripta.png";
    public const string LyraNote = "Personajes/lyra.md";
    public const string Portrait = "_assets/lyra.png";

    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    public static MapState StateWithGrid(bool gridEnabled) => MapState.Empty with
    {
        Grid = new GridSettings(gridEnabled, 70, 0, 0),
        Tokens =
        [
            new MapToken("t1", "Goblin", null, TokenSide.Enemy, 1, 35, 35, null, null),
            new MapToken("t2", "Lyra", LyraNote, TokenSide.Pc, 1, 105, 35, null, null),
        ],
    };

    /// <summary>Mapa con imagen. Con <paramref name="rawState"/> se escribe ese texto tal cual (p. ej. un JSON inválido).</summary>
    public static void WriteImageMap(TempVault vault, MapState? state = null, string? rawState = null, bool withImage = true)
    {
        Write(vault, MapNote, "---\ntype: map\nname: Cripta\nimage: Mapas/cripta.png\n---\n");
        if (withImage) WriteBytes(vault, Image, Png);
        Write(vault, LyraNote, $"---\ntype: pc\nname: Lyra\nportrait: {Portrait}\n---\n");
        WriteBytes(vault, Portrait, Png);
        var statePath = MapPaths.StatePathFor(MapNote);
        if (rawState is not null) Write(vault, statePath, rawState);
        else if (state is not null) Write(vault, statePath, MapStateSerializer.Write(state));
    }

    public static void WriteCanvasMap(TempVault vault) =>
        Write(vault, MapNote, "---\ntype: map\nname: Cripta\ncanvas:\n  width: 1000\n  height: 800\n---\n");

    private static void Write(TempVault vault, string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(vault.PathOf(path))!);
        File.WriteAllText(vault.PathOf(path), text);
    }

    private static void WriteBytes(TempVault vault, string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(vault.PathOf(path))!);
        File.WriteAllBytes(vault.PathOf(path), bytes);
    }
}
