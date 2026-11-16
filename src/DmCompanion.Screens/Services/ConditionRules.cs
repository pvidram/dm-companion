using DmCompanion.Core.Glossary;

namespace DmCompanion.Screens.Services;

/// <summary>Une los estados de un token con su regla en el glosario: el estado guarda <c>prone</c>, el glosario <c>srd:estados/prone</c>.</summary>
public static class ConditionRules
{
    private static readonly string Prefix = GlossaryPacks.SrdKey(GlossaryCategory.Condition, string.Empty);

    /// <summary>Claves de estado con regla en el sistema activo (vacío en un vault sin estados, como Cosmere).</summary>
    public static IReadOnlyCollection<string> Keys(GlossaryService glossary) =>
        glossary.Index.ConditionKeys.Select(key => key.StartsWith(Prefix, StringComparison.Ordinal) ? key[Prefix.Length..] : key).ToList();

    public static string GlossaryKey(string conditionKey) => GlossaryPacks.SrdKey(GlossaryCategory.Condition, conditionKey);
}
