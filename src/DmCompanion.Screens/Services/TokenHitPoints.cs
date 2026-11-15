using DmCompanion.Core.GameSystems;
using DmCompanion.Core.Maps;

namespace DmCompanion.Screens.Services;

/// <summary>Máximo de HP de un token y reglas de combate del sistema activo. Una sola fuente para la tarjeta y el combate.</summary>
public sealed class TokenHitPoints(VaultHost host)
{
    // El genérico no depende del sistema activo: su máximo es su propio hp.
    public ICombatRules Rules => host.Current?.ActiveSystem.Combat ?? BasicCombatRules.Instance;

    /// <summary>Nota → máximo evaluado por el sistema activo; genérico → su hp; nota inexistente → null.</summary>
    public int? MaxFor(MapToken token)
    {
        if (token.Source is not { } source) return token.Hp;
        if (host.Current is not { } vault || vault.Index.Get(source) is not { } entity) return null;
        return vault.ActiveSystem.HitPoints.Max(vault.ActiveSystem.Evaluate(entity.Frontmatter));
    }
}
