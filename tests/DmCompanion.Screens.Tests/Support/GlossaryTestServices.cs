using DmCompanion.Core.Glossary;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Screens.Tests.Support;

internal static class GlossaryTestServices
{
    /// <summary>Glosario suelto (sin DI) con los paquetes reales, para pruebas que construyen los servicios a mano.</summary>
    public static GlossaryService CreateGlossary(VaultHost host) =>
        new(host, new GlossaryPackLoader(new EmbeddedGlossaryPackSource(), NullLogger<GlossaryPackLoader>.Instance), NullLogger<GlossaryService>.Instance);

    /// <summary>Glosario con los paquetes reales embebidos; necesita VaultHost ya registrado.</summary>
    public static IServiceCollection AddGlossaryForTests(this IServiceCollection services)
    {
        services.AddSingleton<IGlossaryPackSource, EmbeddedGlossaryPackSource>();
        services.AddSingleton<GlossaryPackLoader>();
        services.AddSingleton<GlossaryService>();
        return services;
    }
}
