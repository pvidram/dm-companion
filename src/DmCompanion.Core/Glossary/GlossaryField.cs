namespace DmCompanion.Core.Glossary;

public sealed record GlossaryField(string Label, IReadOnlyList<GlossarySpan> Value);
