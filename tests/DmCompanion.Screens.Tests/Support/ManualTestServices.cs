using DmCompanion.Screens.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.Screens.Tests.Support;

internal static class ManualTestServices
{
    public static IServiceCollection AddManualsForTests(this IServiceCollection services)
    {
        services.AddSingleton<ManualLibrary>();
        services.AddSingleton<ManualBookmarkStore>();
        services.AddSingleton<ManualPositions>();
        return services;
    }
}
