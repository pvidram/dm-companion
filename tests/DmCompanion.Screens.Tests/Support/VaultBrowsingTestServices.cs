using DmCompanion.Core.Vault.Storage;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.Screens.Tests.Support;

internal static class VaultBrowsingTestServices
{
    /// <summary>Explorador de carpetas real sobre el disco, con raíz «/» y sin «Pegar una ruta», para el panel «Cambiar vault».</summary>
    public static IServiceCollection AddVaultBrowsingForTests(this IServiceCollection services)
    {
        services.AddSingleton<IFolderBrowser, DirectoryBrowser>();
        services.AddSingleton<IStorageAccess, RootStorageAccess>();
        return services;
    }

    private sealed class RootStorageAccess : IStorageAccess
    {
        public bool HasAllFilesAccess => true;
        public string DefaultBrowseRoot => "/";
        public void RequestAllFilesAccess() { }
    }
}
