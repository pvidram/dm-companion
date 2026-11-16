using DmCompanion.Core.Manuals;

namespace DmCompanion.Screens.Services;

/// <summary>
/// Estado del visor abierto, que <c>ManualViewer</c> publica como valor en cascada a todo lo que hay dentro
/// (paneles laterales, acciones de TV). Es inmutable: el visor crea uno nuevo en cada cambio de página.
/// </summary>
/// <param name="Path">Ruta relativa del PDF en el vault.</param>
/// <param name="Title">Título del manual.</param>
/// <param name="Page">Página actual (1..PageCount).</param>
/// <param name="Doc">Lo que devolvió el visor al abrir: páginas, índice crudo y si tiene texto.</param>
/// <param name="Outline">Índice ya en árbol.</param>
/// <param name="Interop">El interop del visor (la misma instancia scoped que usa <c>ManualViewer</c>): los hijos lo usan desde aquí, p. ej. RenderPngAsync y PickCropAsync, y no inyectan el servicio.</param>
/// <param name="GoToPageAsync">Salta a una página (se ajusta al rango, recuerda la posición y la pinta).</param>
/// <param name="Cropping">Hay un recorte para la TV en curso: el visor no deja cambiar de página.</param>
/// <param name="SetCropping">Lo llama <c>ManualTvActions</c> al entrar en el modo recorte (true) y al salir (false).</param>
public sealed record ManualViewerContext(
    string Path,
    string Title,
    int Page,
    ManualDocInfo Doc,
    IReadOnlyList<OutlineItem> Outline,
    ManualViewerInterop Interop,
    Func<int, Task> GoToPageAsync,
    bool Cropping = false,
    Action<bool>? SetCropping = null);
