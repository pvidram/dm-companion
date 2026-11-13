using Bunit;
using DmCompanion.Screens.Components;
using DmCompanion.UI;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.Screens.Tests;

public sealed class WelcomeScreenTests : BunitContext, IAsyncLifetime
{
    public WelcomeScreenTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddDmCompanionUI();
    }

    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

    async Task IAsyncLifetime.DisposeAsync() => await base.DisposeAsync();

    private static AngleSharp.Dom.IElement ButtonWith(IRenderedComponent<WelcomeScreen> cut, string text) =>
        cut.FindAll("button").First(b => b.TextContent.Contains(text));

    [Fact]
    public void Shows_the_title()
    {
        Render<WelcomeScreen>().Markup.Should().Contain("Bienvenido a DM Companion");
    }

    [Fact]
    public void Shows_both_buttons()
    {
        var cut = Render<WelcomeScreen>();

        cut.FindAll("button").Select(b => b.TextContent.Trim())
            .Should().BeEquivalentTo("Probar con el vault de ejemplo", "Abrir mi vault");
    }

    [Fact]
    public void Open_own_button_emits_its_callback()
    {
        var calls = 0;
        var cut = Render<WelcomeScreen>(p => p.Add(c => c.OnOpenOwn, () => { calls++; }));

        ButtonWith(cut, "Abrir mi vault").Click();

        calls.Should().Be(1);
    }

    [Fact]
    public void Try_sample_button_emits_its_callback()
    {
        var calls = 0;
        var cut = Render<WelcomeScreen>(p => p.Add(c => c.OnTrySample, () => { calls++; }));

        ButtonWith(cut, "Probar con el vault de ejemplo").Click();

        calls.Should().Be(1);
    }

    [Fact]
    public void Shows_the_error_notice()
    {
        var cut = Render<WelcomeScreen>(p => p.Add(c => c.Error, "No se pudo preparar el vault de ejemplo."));

        cut.Markup.Should().Contain("No se pudo preparar el vault de ejemplo.");
    }

    [Fact]
    public void Without_error_shows_no_notice()
    {
        Render<WelcomeScreen>().Markup.Should().NotContain("ds-notice");
    }
}
