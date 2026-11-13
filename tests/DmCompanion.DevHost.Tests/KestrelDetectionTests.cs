using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.DevHost.Tests;

public sealed class KestrelDetectionTests
{
    [Fact]
    public void IsKestrel_is_true_for_the_real_server_implementation()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options => options.Listen(System.Net.IPAddress.Loopback, 0));
        using var app = builder.Build();

        KestrelDetection.IsKestrel(app.Services.GetRequiredService<IServer>()).Should().BeTrue();
    }

    [Fact]
    public async Task IsKestrel_is_false_for_the_in_memory_test_server()
    {
        await using var factory = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>();
        using var _ = factory.CreateClient();

        KestrelDetection.IsKestrel(factory.Services.GetRequiredService<IServer>()).Should().BeFalse();
    }
}
