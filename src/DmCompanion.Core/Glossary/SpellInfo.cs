namespace DmCompanion.Core.Glossary;

public sealed record SpellInfo(int Level, string SchoolKey, IReadOnlyList<string> ClassKeys, bool Ritual, bool Concentration,
    string Header, string CastingTime, string Range, string Components, string Duration);
