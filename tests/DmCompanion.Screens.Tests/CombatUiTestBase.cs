using AngleSharp.Dom;
using Bunit;
using DmCompanion.Core.Combat;
using DmCompanion.Core.Maps;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.Screens.Tests;

public abstract class CombatUiTestBase : TokenTestBase
{
    protected CombatUiTestBase()
    {
        Services.AddSingleton<MapLibrary>();
        Services.AddScoped<WikilinkInterop>();
    }

    protected CombatController Combat => Services.GetRequiredService<CombatController>();

    protected async Task<IRenderedComponent<DmShell>> RenderAsync(MapToken? ana = null, bool start = true, params MapToken[] more)
    {
        await OpenMapAsync(MapState.Empty with
        {
            Tokens = [ana ?? GenericToken("a", "Ana", hp: 20, side: TokenSide.Pc), GenericToken("b", "Bruno", hp: 20), .. more],
        });
        if (start)
        {
            Combat.Start([new InitiativeEntry("a", 15), new InitiativeEntry("b", 10)]);
            await Session.FlushAsync();
        }

        return await RenderShellAsync();
    }

    protected MapToken Ana => Tokens.First(t => t.Id == "a");

    protected static IElement Select(IRenderedComponent<DmShell> cut, string testId) => cut.Find($"select[data-testid={testId}]");

    /// <summary>Simula un mapa de solo lectura: no hay otra forma de tener combate y solo lectura a la vez.</summary>
    protected void MakeReadOnly() =>
        typeof(MapSession).GetProperty(nameof(MapSession.IsReadOnly))!.SetValue(Session, true);
}
