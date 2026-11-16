namespace DmCompanion.Core.Glossary;

public sealed class EmbeddedGlossaryPackSource : IGlossaryPackSource
{
    public Stream? Open(string packName) =>
        typeof(EmbeddedGlossaryPackSource).Assembly.GetManifestResourceStream($"glossary/{packName}.json");
}
