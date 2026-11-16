using DmCompanion.Core.GameSystems;
using DmCompanion.Core.Glossary;
using DmCompanion.Core.Maps;

namespace DmCompanion.Screens.Services;

/// <summary>Máximo de HP de un token y reglas de combate del sistema activo. Una sola fuente para la tarjeta y el combate.</summary>
public sealed class TokenHitPoints(VaultHost host, GlossaryService glossary)
{
    // El genérico no depende del sistema activo: su máximo es su propio hp.
    public ICombatRules Rules => host.Current?.ActiveSystem.Combat ?? BasicCombatRules.Instance;

    /// <summary>Nota o criatura del SRD → máximo evaluado por el sistema activo; genérico → su hp; nota o entrada inexistente → null.</summary>
    public int? MaxFor(MapToken token)
    {
        if (token.Source is not { } source) return token.Hp;
        if (host.Current is not { } vault) return null;
        // Frontmatter reconstruye el diccionario en cada lectura: una sola vez.
        if (TokenSourceResolver.Resolve(source, vault.Index, glossary.GetSrd).Frontmatter is not { } frontmatter) return null;
        return vault.ActiveSystem.HitPoints.Max(vault.ActiveSystem.Evaluate(frontmatter));
    }
}
