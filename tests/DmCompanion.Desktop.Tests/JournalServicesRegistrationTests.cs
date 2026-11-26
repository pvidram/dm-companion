using System.Reflection;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Platform;
using DmCompanion.Screens.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.Desktop.Tests;

public sealed class JournalServicesRegistrationTests : IDisposable
{
    private readonly TempVault _vault = TempVault.FromDemo();
    private readonly TempVault _data = TempVault.Empty();
    private readonly TempVault _documents = TempVault.Empty();
    private readonly WebApplicationFactory<Program> _factory;

    public JournalServicesRegistrationTests() =>
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Desktop:SingleInstance", "false");
            builder.UseSetting("Desktop:DataRoot", _data.Root);
            builder.UseSetting("Desktop:DocumentsRoot", _documents.Root);
            builder.UseSetting("Desktop:SampleSource", TempVault.FindDemo());
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IAppPreferences, InMemoryAppPreferences>();
                services.AddSingleton<IBrowserLauncher>(new FakeBrowserLauncher());
                services.AddSingleton<ILogFolder>(new FakeLogFolder());
            });
        });

    public void Dispose()
    {
        _factory.Dispose();
        _documents.Dispose();
        _data.Dispose();
        _vault.Dispose();
    }

    public static TheoryData<Type> JournalComponents =>
    [
        typeof(JournalTab), typeof(JournalEntryDetail), typeof(JournalFormDialog), typeof(JournalDeleteDialog),
        typeof(JournalFormImages), typeof(JournalFormCreature), typeof(JournalLogSection), typeof(JournalCreatureSection),
        typeof(JournalThumbnail), typeof(JournalTvBar),
        typeof(ShowJournalOnTvButton), typeof(GlossaryDetail), typeof(PlayersPanel), typeof(CharacterSheet), typeof(DmShell),
    ];

    [Theory]
    [MemberData(nameof(JournalComponents))]
    public async Task Every_service_injected_by_the_component_resolves_in_a_scope(Type component)
    {
        await using var scope = _factory.Services.CreateAsyncScope();

        var unresolved = InjectedTypes(component).Where(t => scope.ServiceProvider.GetService(t) is null).ToList();

        unresolved.Should().BeEmpty();
    }

    [Fact]
    public void The_journal_services_are_registered_as_singletons()
    {
        var services = new object?[]
        {
            _factory.Services.GetService<JournalStore>(), _factory.Services.GetService<JournalIndex>(), _factory.Services.GetService<JournalLogService>(),
        };

        services.Should().NotContainNulls();
    }

    [Fact]
    public async Task The_registered_store_creates_an_entry_and_the_log_includes_it()
    {
        await _factory.Services.GetRequiredService<VaultHost>().OpenAsync(_vault.Root);
        var store = _factory.Services.GetRequiredService<JournalStore>();
        var image = new JournalImageSource("a.png", () => Task.FromResult<Stream>(new MemoryStream([1, 2, 3])));

        var created = await store.CreateAsync(new JournalCreate("Personajes/Lyra.md", "Ankheg", "Nota.", null, [image]));
        var log = await _factory.Services.GetRequiredService<JournalLogService>().GenerateAsync();

        (created.Status, Directory.GetFiles(_vault.PathOf("Personajes/Lyra/Diario")).Length, log.Status, log.Entries, File.Exists(_vault.PathOf("Diario de la campaña.md")))
            .Should().Be((JournalStatus.Done, 2, JournalLogStatus.Generated, 1, true));
    }

    // IncomingShare no se registra en el DevHost ni en el PC: no tienen «Compartir» (los componentes lo piden con GetService).
    [Fact]
    public void There_is_no_share_inbox_in_this_host() =>
        _factory.Services.GetService<IncomingShare>().Should().BeNull();

    // Incluye los [Inject] privados y los de las clases base (DmComponentBase, JournalComponentBase).
    private static IEnumerable<Type> InjectedTypes(Type component)
    {
        for (var type = component; type is not null && type != typeof(ComponentBase); type = type.BaseType)
        {
            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (property.GetCustomAttribute<InjectAttribute>() is not null)
                {
                    yield return property.PropertyType;
                }
            }
        }
    }
}
