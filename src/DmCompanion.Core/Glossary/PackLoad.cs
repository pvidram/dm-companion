namespace DmCompanion.Core.Glossary;

/// <summary>Error: mensaje en español para Diagnóstico, sin texto de la fuente.</summary>
public sealed record PackLoad<T>(T Value, string? Error);
