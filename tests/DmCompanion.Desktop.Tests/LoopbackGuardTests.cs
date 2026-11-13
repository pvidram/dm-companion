namespace DmCompanion.Desktop.Tests;

public sealed class LoopbackGuardTests
{
    [Theory]
    [InlineData("http://127.0.0.1:5190")]
    [InlineData("http://localhost:5190")]
    [InlineData("http://[::1]:5190")]
    public void IsLoopbackOnly_accepts_loopback_addresses(string address) =>
        LoopbackGuard.IsLoopbackOnly([address]).Should().BeTrue();

    [Theory]
    [InlineData("http://0.0.0.0:5190")]
    [InlineData("http://*:5190")]
    [InlineData("http://192.168.1.10:5190")]
    [InlineData("http://+:5190")]
    public void IsLoopbackOnly_rejects_other_addresses(string address) =>
        LoopbackGuard.IsLoopbackOnly([address]).Should().BeFalse();

    [Fact]
    public void IsLoopbackOnly_rejects_a_list_with_one_exposed_address() =>
        LoopbackGuard.IsLoopbackOnly(["http://127.0.0.1:5190", "http://0.0.0.0:5190"]).Should().BeFalse();

    [Fact]
    public void IsLoopbackOnly_rejects_an_empty_set() =>
        LoopbackGuard.IsLoopbackOnly([]).Should().BeFalse();
}
