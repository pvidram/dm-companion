namespace DmCompanion.Core.GameSystems;

/// <summary>Estado de salud aproximado que ven los jugadores de un enemigo o aliado; lo comparten todas las reglas.</summary>
public static class HealthLabels
{
    public static string? For(int current, int? max)
    {
        if (max is not { } m || m <= 0 || current <= 0) return null;
        if (current >= m) return "Ileso";
        return current * 2 > m ? "Herido" : "Malherido";
    }
}
