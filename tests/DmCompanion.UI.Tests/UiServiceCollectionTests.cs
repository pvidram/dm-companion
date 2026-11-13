using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.UI.Tests;

public class UiServiceCollectionTests
{
    [Fact]
    public void Add_dm_companion_ui_registers_the_system_time_provider()
    {
        using var provider = new ServiceCollection().AddDmCompanionUI().BuildServiceProvider();
        provider.GetRequiredService<TimeProvider>().Should().BeSameAs(TimeProvider.System);
    }
}
