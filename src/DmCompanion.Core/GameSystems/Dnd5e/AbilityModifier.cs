namespace DmCompanion.Core.GameSystems;

public static class AbilityModifier
{
    public static int Of(int score) => (int)Math.Floor((score - 10) / 2.0);
}
