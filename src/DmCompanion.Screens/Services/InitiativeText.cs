using System.Globalization;

namespace DmCompanion.Screens.Services;

/// <summary>Iniciativa tecleada: vacío es «sin número» y lo demás ha de ser un entero (negativo vale: −1 es una tirada posible).</summary>
public static class InitiativeText
{
    public const string Error = "Escribe un número entero";

    public static bool TryParse(string? text, out int? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text)) return true;
        if (!int.TryParse(text.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed)) return false;
        value = parsed;
        return true;
    }
}
