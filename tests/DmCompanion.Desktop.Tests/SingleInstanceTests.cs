using Microsoft.Extensions.Time.Testing;

namespace DmCompanion.Desktop.Tests;

public sealed class SingleInstanceTests : IDisposable
{
    private readonly TempVault _folder = TempVault.Empty();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void WriteUrl_and_ReadUrl_round_trip()
    {
        var file = _folder.PathOf("sub/instance.json");

        SingleInstance.WriteUrl(file, "http://localhost:5190");

        SingleInstance.ReadUrl(file).Should().Be("http://localhost:5190");
    }

    [Fact]
    public void ReadUrl_returns_null_for_a_missing_file() =>
        SingleInstance.ReadUrl(_folder.PathOf("no-existe.json")).Should().BeNull();

    [Fact]
    public void ReadUrl_returns_null_for_broken_json()
    {
        var file = _folder.PathOf("instance.json");
        File.WriteAllText(file, "{ esto no es json");

        SingleInstance.ReadUrl(file).Should().BeNull();
    }

    [Fact]
    public void TryAcquire_returns_null_while_another_instance_holds_the_name()
    {
        var name = @"Local\DmCompanion.Tests." + Guid.NewGuid().ToString("N");
        using var first = SingleInstance.TryAcquire(name);

        var second = SingleInstance.TryAcquire(name);

        (first is not null && second is null).Should().BeTrue();
    }

    [Fact]
    public async Task WaitForUrlAsync_returns_the_url_written_after_the_wait_started()
    {
        var file = _folder.PathOf("instance.json");
        var time = new FakeTimeProvider();
        var waiting = SingleInstance.WaitForUrlAsync(file, time, TimeSpan.FromSeconds(10));
        time.Advance(TimeSpan.FromMilliseconds(500));
        SingleInstance.WriteUrl(file, "http://localhost:5190");

        // El temporizador de Task.Delay se registra de forma asíncrona: se avanza el reloj hasta que termine.
        while (!waiting.IsCompleted)
        {
            time.Advance(TimeSpan.FromMilliseconds(250));
            await Task.Delay(5);
        }

        (await waiting).Should().Be("http://localhost:5190");
    }

    [Fact]
    public async Task WaitForUrlAsync_returns_null_when_the_url_never_appears()
    {
        var time = new FakeTimeProvider();
        var waiting = SingleInstance.WaitForUrlAsync(_folder.PathOf("instance.json"), time, TimeSpan.FromSeconds(10));

        while (!waiting.IsCompleted)
        {
            time.Advance(TimeSpan.FromSeconds(1));
            await Task.Delay(5);
        }

        (await waiting).Should().BeNull();
    }

    [Fact]
    public void DeleteUrlIfOwner_keeps_the_file_of_the_running_instance_when_this_one_is_not_the_owner()
    {
        var file = _folder.PathOf("instance.json");
        SingleInstance.WriteUrl(file, "http://localhost:5190");

        SingleInstance.DeleteUrlIfOwner(null, file);

        File.Exists(file).Should().BeTrue();
    }

    [Fact]
    public void DeleteUrlIfOwner_deletes_the_file_when_this_instance_owns_the_mutex()
    {
        var file = _folder.PathOf("instance.json");
        SingleInstance.WriteUrl(file, "http://localhost:5190");
        using var owner = SingleInstance.TryAcquire(@"Local\DmCompanion.Tests." + Guid.NewGuid().ToString("N"));

        SingleInstance.DeleteUrlIfOwner(owner, file);

        File.Exists(file).Should().BeFalse();
    }

    [Fact]
    public void TryAcquire_removes_a_stale_instance_file()
    {
        var file = _folder.PathOf("instance.json");
        SingleInstance.WriteUrl(file, "http://localhost:1");

        using var instance = SingleInstance.TryAcquire(@"Local\DmCompanion.Tests." + Guid.NewGuid().ToString("N"), file);

        File.Exists(file).Should().BeFalse();
    }
}
