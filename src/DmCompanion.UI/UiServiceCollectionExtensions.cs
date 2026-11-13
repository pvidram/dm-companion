using DmCompanion.UI.Interop;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DmCompanion.UI;

public static class UiServiceCollectionExtensions
{
    public static IServiceCollection AddDmCompanionUI(this IServiceCollection services)
    {
        // TryAdd: si el host ya registró un TimeProvider (p. ej. uno falso en tests), se respeta.
        services.TryAddSingleton(TimeProvider.System);
        return services.AddScoped<IFocusInterop, FocusInterop>();
    }
}
