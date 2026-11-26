namespace DmCompanion.Core.Journal;

/// <param name="Name">Nombre relativo a la carpeta Diario/.</param>
/// <param name="Path">Ruta relativa al vault (carpeta + nombre).</param>
/// <param name="Exists">Si el fichero existe en el vault.</param>
/// <param name="Displayable">jpg, jpeg, png, webp o gif. HEIC se guarda pero no se muestra.</param>
public sealed record JournalImage(string Name, string Path, bool Exists, bool Displayable);
