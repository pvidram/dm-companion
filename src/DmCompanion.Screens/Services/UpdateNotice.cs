using DmCompanion.Screens.Platform;

namespace DmCompanion.Screens.Services;

/// <summary>
/// Estado del aviso de versión nueva: respeta las preferencias, compara versiones y guarda el aviso pendiente.
/// Solo lo registra el host de escritorio.
/// </summary>
public sealed class UpdateNotice(IAppPreferences preferences, IUpdateCheck? check)
{
    public const string CheckKey = "update.check";
    public const string SkipKey = "update.skipVersion";

    public event Action? Changed;

    public bool IsAvailable => check is not null;

    public AvailableUpdate? Pending { get; private set; }

    public bool Enabled
    {
        get => preferences.Get(CheckKey) != "false";
        set => preferences.Set(CheckKey, value ? "true" : "false");
    }

    public async Task RefreshAsync(string currentVersion, CancellationToken ct)
    {
        if (check is null || !Enabled) return;
        var latest = await check.GetLatestAsync(ct);
        if (latest is null || latest.Version == preferences.Get(SkipKey)) return;
        if (ReleaseVersion.Compare(latest.Version, currentVersion) is not > 0) return;
        Pending = latest;
        Changed?.Invoke();
    }

    public void Dismiss() => Clear();

    public void SkipPending()
    {
        if (Pending is { } pending) preferences.Set(SkipKey, pending.Version);
        Clear();
    }

    public void OpenPending()
    {
        if (Pending is { } pending) check?.OpenReleasePage(pending);
    }

    private void Clear()
    {
        Pending = null;
        Changed?.Invoke();
    }
}
