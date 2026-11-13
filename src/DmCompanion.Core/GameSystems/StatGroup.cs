namespace DmCompanion.Core.GameSystems;

public enum GroupOrder { Definition, Alphabetical }

public sealed record StatGroup(string Id, string Label, GroupOrder Order);
