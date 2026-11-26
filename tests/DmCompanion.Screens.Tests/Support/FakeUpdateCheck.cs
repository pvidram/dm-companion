using DmCompanion.Screens.Platform;

namespace DmCompanion.Screens.Tests.Support;

/// <summary>Consulta de versión falsa: devuelve la release configurada y anota las llamadas.</summary>
internal sealed class FakeUpdateCheck : IUpdateCheck
{
    public AvailableUpdate? Latest { get; set; }
    public int Calls { get; private set; }
    public List<AvailableUpdate> Opened { get; } = [];

    public Task<AvailableUpdate?> GetLatestAsync(CancellationToken ct)
    {
        Calls++;
        return Task.FromResult(Latest);
    }

    public void OpenReleasePage(AvailableUpdate update) => Opened.Add(update);
}
