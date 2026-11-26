namespace DmCompanion.Screens.Platform;

/// <summary>Fichero recibido de «Compartir», ya copiado a la carpeta temporal.</summary>
public sealed record SharedFile(string FileName, string TempPath);

/// <summary>
/// Plataforma: carpeta temporal (la caché de la app) donde se copian los ficheros compartidos antes de guardarlos en el vault.
/// Solo la registra la App de Android; el DevHost y la versión de PC no tienen «Compartir».
/// </summary>
public interface IShareInbox
{
    string TempFolder { get; }
}
