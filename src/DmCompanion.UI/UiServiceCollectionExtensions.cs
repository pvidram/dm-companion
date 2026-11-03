using DmCompanion.UI.Interop;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.UI;

public static class UiServiceCollectionExtensions
{
    public static IServiceCollection AddDmCompanionUI(this IServiceCollection services) =>
        services.AddScoped<IFocusInterop, FocusInterop>();
}
