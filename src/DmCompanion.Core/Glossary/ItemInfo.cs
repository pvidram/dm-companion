namespace DmCompanion.Core.Glossary;

public sealed record ItemInfo(string CategoryKey, IReadOnlyList<string> RarityKeys, bool Attunement, string Header);
