using DmCompanion.Core.Rendering;
using DmCompanion.Core.Vault;

namespace DmCompanion.Screens.Services;

/// <summary>Decide a dónde apunta un wikilink: una nota del vault gana al glosario del SRD, y si no hay ninguna es un enlace roto.</summary>
public static class WikilinkClassifier
{
    public static WikilinkKind Classify(VaultIndex notes, GlossaryService glossary, string target) =>
        notes.Resolve(target) is not null ? WikilinkKind.Note
        : glossary.Index.TryResolve(target) is not null ? WikilinkKind.Glossary
        : WikilinkKind.Broken;
}
