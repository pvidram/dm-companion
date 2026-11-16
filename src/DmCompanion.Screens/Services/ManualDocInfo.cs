using DmCompanion.Core.Manuals;

namespace DmCompanion.Screens.Services;

/// <summary>Lo que devuelve el visor al abrir un PDF: páginas, índice crudo (nivel 0 = raíz) y si tiene texto que buscar.</summary>
public sealed record ManualDocInfo(int PageCount, IReadOnlyList<OutlineEntry> Outline, bool HasText);
