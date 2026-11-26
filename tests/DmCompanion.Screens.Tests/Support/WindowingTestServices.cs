using DmCompanion.Screens.Platform;
using DmCompanion.Screens.Services;
using DmCompanion.UI.Interop;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DmCompanion.Screens.Tests.Support;

internal static class WindowingTestServices
{
    /// <summary>Gestor de ventanas, almacén de disposición y un interop de marcos falso (sin JS).</summary>
    public static IServiceCollection AddWindowingForTests(this IServiceCollection services)
    {
        services.TryAddSingleton<IAppPreferences>(_ => new InMemoryAppPreferences());
        services.AddSingleton<FakeWindowFrameInterop>();
        services.AddSingleton<IWindowFrameInterop>(sp => sp.GetRequiredService<FakeWindowFrameInterop>());
        services.AddScoped<PanelLayoutStore>();
        services.AddScoped<WindowManager>();
        return services;
    }
}
