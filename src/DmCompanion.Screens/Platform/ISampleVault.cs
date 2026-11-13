namespace DmCompanion.Screens.Platform;

/// <summary>
/// Vault de ejemplo opcional. Solo lo registra el host de escritorio; App y DevHost no, y Screens lo pide con GetService.
/// </summary>
public interface ISampleVault
{
    bool IsAvailable { get; }

    /// <summary>Deja el vault de ejemplo listo (copiándolo si falta) y devuelve su ruta.</summary>
    Task<string> PrepareAsync();
}
