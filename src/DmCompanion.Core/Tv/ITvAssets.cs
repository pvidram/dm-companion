namespace DmCompanion.Core.Tv;

/// <summary>Recursos fijos de la página de la TV (HTML, CSS, JS y fuentes). Solo responde a nombres conocidos.</summary>
public interface ITvAssets
{
    /// <summary>El recurso o null si no existe.</summary>
    Stream? Open(string name);

    string ContentType(string name);
}
