using DmCompanion.UI.Interop;

namespace DmCompanion.Screens.Services;

/// <summary>Tarjetas de criatura abiertas a la vez: sustitución, chincheta, edición y orden de apilado.</summary>
public sealed class WindowManager : IDisposable
{
    private readonly ShellState _shell;
    private readonly MapSession _session;
    private readonly PanelLayoutStore _layout;
    private readonly List<TokenCardWindow> _cards = [];
    private string? _lastMapPath;
    private long _order;

    public WindowManager(ShellState shell, MapSession session, PanelLayoutStore layout)
    {
        _shell = shell;
        _session = session;
        _layout = layout;
        _lastMapPath = session.Current?.NotePath;
        session.Changed += OnSessionChanged;
    }

    public IReadOnlyList<TokenCardWindow> TokenCards => [.. _cards.OrderBy(c => c.Order)];

    public event Action? Changed;

    public void OpenTokenCard(string tokenId, double x, double y)
    {
        if (_cards.Any(c => c.TokenId == tokenId))
        {
            BringToFront(tokenId);
            return;
        }

        var oldest = _cards.OrderBy(c => c.Order).FirstOrDefault(c => !c.IsPinned && !c.IsEditing);
        if (oldest is not null) _cards.Remove(oldest);
        _cards.Add(new TokenCardWindow(tokenId, x, y, _layout.GetSize(LayoutKeys.TokenCard), false, false, ++_order));
        if (!_layout.IsPinned(LayoutKeys.History)) _shell.CloseHistory();
        Changed?.Invoke();
    }

    public void Close(string tokenId)
    {
        if (_cards.RemoveAll(c => c.TokenId == tokenId) > 0) Changed?.Invoke();
    }

    public void SetPinned(string tokenId, bool pinned) => Update(tokenId, c => c with { IsPinned = pinned });

    public void SetEditing(string tokenId, bool editing) => Update(tokenId, c => c with { IsEditing = editing });

    public void BringToFront(string tokenId)
    {
        if (_cards.Count > 0 && _cards.MaxBy(c => c.Order)!.TokenId == tokenId) return;
        Update(tokenId, c => c with { Order = ++_order });
    }

    public void UpdateFrame(string tokenId, WindowFrameChange change)
    {
        if (!_cards.Any(c => c.TokenId == tokenId)) return;
        if (change.Size is { } size) _layout.SetSize(LayoutKeys.TokenCard, size);
        Update(tokenId, c => c with { X = change.X, Y = change.Y, Size = change.Size is null ? c.Size : _layout.GetSize(LayoutKeys.TokenCard) });
    }

    public void ResetSize(string tokenId)
    {
        if (!_cards.Any(c => c.TokenId == tokenId)) return;
        _layout.ResetSize(LayoutKeys.TokenCard);
        Update(tokenId, c => c with { Size = null });
    }

    public void CloseAll()
    {
        if (_cards.Count == 0) return;
        _cards.Clear();
        Changed?.Invoke();
    }

    public void Dispose() => _session.Changed -= OnSessionChanged;

    private void Update(string tokenId, Func<TokenCardWindow, TokenCardWindow> change)
    {
        var i = _cards.FindIndex(c => c.TokenId == tokenId);
        if (i < 0) return;
        var updated = change(_cards[i]);
        if (updated == _cards[i]) return;
        _cards[i] = updated;
        Changed?.Invoke();
    }

    private void OnSessionChanged(object? sender, EventArgs e)
    {
        var path = _session.Current?.NotePath;
        if (string.Equals(path, _lastMapPath, StringComparison.Ordinal)) return;
        _lastMapPath = path;
        CloseAll();
    }
}
