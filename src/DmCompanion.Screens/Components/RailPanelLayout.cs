using DmCompanion.Screens.Services;
using DmCompanion.UI.Interop;

namespace DmCompanion.Screens.Components;

/// <summary>Une un panel del raíl con el tamaño recordado: lo lee, lo guarda al soltar el tirador y lo olvida con doble toque.</summary>
public sealed class RailPanelLayout(PanelLayoutStore store, Action notify)
{
    public WindowSize? FrameSize(string key) => store.GetSize(key);

    public void OnChanged(string key, WindowFrameChange change)
    {
        if (change.Size is not { } size) return;
        store.SetSize(key, size);
        notify();
    }

    public void OnReset(string key)
    {
        store.ResetSize(key);
        notify();
    }
}
