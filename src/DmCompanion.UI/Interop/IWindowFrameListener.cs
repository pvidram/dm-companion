namespace DmCompanion.UI.Interop;

public interface IWindowFrameListener
{
    Task OnFrameChangedAsync(WindowFrameChange change);
    Task OnFrameResetAsync();
}
