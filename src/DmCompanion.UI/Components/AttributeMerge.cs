namespace DmCompanion.UI.Components;

/// <summary>Combina las clases propias de un componente con la <c>class</c> que el llamador pase en AdditionalAttributes.</summary>
internal static class AttributeMerge
{
    public static string Class(string ownClasses, IReadOnlyDictionary<string, object>? additionalAttributes) =>
        additionalAttributes is not null
        && additionalAttributes.TryGetValue("class", out var extra)
        && Convert.ToString(extra, System.Globalization.CultureInfo.InvariantCulture) is { Length: > 0 } extraClasses
            ? ownClasses + " " + extraClasses
            : ownClasses;
}
