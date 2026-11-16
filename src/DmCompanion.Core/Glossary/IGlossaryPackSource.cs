namespace DmCompanion.Core.Glossary;

public interface IGlossaryPackSource
{
    /// <summary>packName sin extensión: "indice", "criaturas"…; null si no existe.</summary>
    Stream? Open(string packName);
}
