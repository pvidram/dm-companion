using DmCompanion.Core.Maps;

namespace DmCompanion.Core.Combat;

/// <summary>Marcar y revivir tokens. Solo toca <c>Dead</c>: ni HP ni salvaciones. Si nada cambia devuelve el mismo estado.</summary>
public static class DeathOps
{
    public static MapState MarkDead(MapState s, string tokenId) => Set(s, tokenId, true);

    // Dead = null y no false, para que el campo se omita en el JSON.
    public static MapState Revive(MapState s, string tokenId) => Set(s, tokenId, false);

    private static MapState Set(MapState s, string tokenId, bool dead)
    {
        var token = s.Tokens.FirstOrDefault(t => t.Id == tokenId);
        if (token is null || token.IsDead == dead) return s;
        var value = dead ? true : (bool?)null;
        return s with { Tokens = s.Tokens.Select(t => t.Id == tokenId ? t with { Dead = value } : t).ToList() };
    }
}
