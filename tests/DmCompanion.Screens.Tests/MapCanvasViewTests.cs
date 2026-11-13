using Bunit;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.Screens.Tests;

/// <summary>Consultas de solo lectura al lienzo: centro de la vista y rectángulo del área del mapa.</summary>
public sealed class MapCanvasViewTests : TokenTestBase
{
    private MapCanvasInterop Interop => Services.GetRequiredService<MapCanvasInterop>();

    private async Task LoadedAsync()
    {
        await OpenMapAsync();
        await Interop.LoadAsync(default);
    }

    [Fact]
    public async Task View_center_comes_from_the_canvas()
    {
        JSInterop.Setup<double[]>("mapCanvas.viewCenter").SetResult([500, 400]);
        await LoadedAsync();

        (await Interop.ViewCenterAsync()).Should().Be((500.0, 400.0));
    }

    [Fact]
    public async Task View_center_is_null_before_the_canvas_is_loaded()
    {
        await OpenMapAsync();

        (await Interop.ViewCenterAsync()).Should().BeNull();
    }

    [Theory]
    [InlineData(new double[] { 1 })]
    [InlineData(new double[] { 1, 2, 3 })]
    [InlineData(new[] { double.NaN, 2 })]
    [InlineData(new[] { 1, double.PositiveInfinity })]
    public async Task View_center_ignores_a_malformed_answer(double[] answer)
    {
        JSInterop.Setup<double[]>("mapCanvas.viewCenter").SetResult(answer);
        await LoadedAsync();

        (await Interop.ViewCenterAsync()).Should().BeNull();
    }

    [Fact]
    public async Task Host_rect_comes_from_the_canvas()
    {
        JSInterop.Setup<double[]>("mapCanvas.hostRect").SetResult([100, 50, 1000, 800]);
        await LoadedAsync();

        (await Interop.HostRectAsync()).Should().Be(new MapAreaRect(100, 50, 1000, 800));
    }

    [Theory]
    [InlineData(new double[] { 1, 2, 3 })]
    [InlineData(new double[] { 0, 0, 0, 800 })]
    [InlineData(new[] { 0, 0, double.NaN, 800 })]
    public async Task Host_rect_ignores_a_malformed_answer(double[] answer)
    {
        JSInterop.Setup<double[]>("mapCanvas.hostRect").SetResult(answer);
        await LoadedAsync();

        (await Interop.HostRectAsync()).Should().BeNull();
    }
}
