namespace DmCompanion.Desktop.Window;

/// <summary>Trae la ventana de la app al frente (restaurándola si estaba minimizada).</summary>
public interface IWindowActivator
{
    void BringToFront();
}
