using Microsoft.AspNetCore.Components;

namespace DmCompanion.UI.Interop;

/// <summary>Mueve el foco a un contenedor (y lo atrapa si <paramref name="trap"/>) y lo devuelve al salir.</summary>
public interface IFocusInterop
{
    ValueTask EnterAsync(ElementReference container, bool trap);
    ValueTask ExitAsync(ElementReference container);
}
