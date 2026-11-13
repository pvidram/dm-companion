using System.Globalization;

namespace DmCompanion.Core.Maps;

/// <summary>Resultado de interpretar el texto escrito en la fila de HP. Con Ok = false, Value no significa nada.</summary>
public readonly record struct HpEntryResult(bool Ok, int Value);

/// <summary>Interpreta lo que el DM escribe en los HP: «12» fija, «-5» resta (daño), «+3» suma (curación).</summary>
public static class HpEntry
{
    /// <summary>Un texto que no es un entero (con o sin signo, o fuera de rango) es un resultado inválido, no una excepción.</summary>
    public static HpEntryResult Apply(int current, int max, string? text)
    {
        // El aviso de la tarjeta enseña «−» (U+2212), así que se acepta igual que «-».
        var trimmed = text?.Trim().Replace('\u2212', '-');
        if (string.IsNullOrEmpty(trimmed)
            || !int.TryParse(trimmed, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number))
        {
            return new HpEntryResult(false, 0);
        }

        // long: current + number no desborda aunque ambos estén cerca de int.MaxValue.
        long result = trimmed[0] is '+' or '-' ? (long)current + number : number;
        return new HpEntryResult(true, (int)Math.Clamp(result, 0, Math.Max(max, 0)));
    }
}
