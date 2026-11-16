using System.Reflection;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Platform;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.DevHost.Tests;

public sealed class ManualServicesRegistrationTests : IDisposable
{
    private readonly TempVault _vault = TempVault.FromDemo();
    private readonly WebApplicationFactory<Program> _factory;

    public ManualServicesRegistrationTests() =>
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("DevHost:VaultPath", _vault.Root);
            builder.ConfigureTestServices(services => services.AddSingleton<IAppPreferences, InMemoryAppPreferences>());
        });

    public void Dispose()
    {
        _factory.Dispose();
        _vault.Dispose();
    }

    public static TheoryData<Type> ManualComponents =>
    [
        typeof(ManualViewer), typeof(ManualsTab), typeof(ManualOutlinePane), typeof(ManualSearchPane),
        typeof(ManualBookmarksPane), typeof(ManualTvActions), typeof(GlossaryPanel),
    ];

    [Theory]
    [MemberData(nameof(ManualComponents))]
    public async Task Every_service_injected_by_the_component_resolves_in_a_scope(Type component)
    {
        // Asíncrono porque ManualViewerInterop solo implementa IAsyncDisposable.
        await using var scope = _factory.Services.CreateAsyncScope();

        var unresolved = InjectedTypes(component).Where(t => scope.ServiceProvider.GetService(t) is null).ToList();

        unresolved.Should().BeEmpty();
    }

    private static IEnumerable<Type> InjectedTypes(Type component) =>
        component.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(p => p.GetCustomAttribute<InjectAttribute>() is not null)
            .Select(p => p.PropertyType);
}
