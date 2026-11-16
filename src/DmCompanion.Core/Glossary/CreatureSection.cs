namespace DmCompanion.Core.Glossary;

public sealed record CreatureSection(string Key, string Title, IReadOnlyList<GlossarySpan>? Intro, IReadOnlyList<CreatureFeature> Items);
