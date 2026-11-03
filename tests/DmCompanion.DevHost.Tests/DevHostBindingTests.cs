namespace DmCompanion.DevHost.Tests;

public sealed class DevHostBindingTests
{
    [Theory]
    [InlineData("http://127.0.0.1:5180")]
    [InlineData("http://localhost:5180")]
    [InlineData("http://[::1]:5180")]
    public void IsLoopbackOnly_accepts_loopback_addresses(string address) =>
        DevHostBinding.IsLoopbackOnly([address]).Should().BeTrue();

    [Theory]
    [InlineData("http://0.0.0.0:5180")]
    [InlineData("http://*:5180")]
    [InlineData("http://192.168.1.10:5180")]
    [InlineData("http://+:5180")]
    public void IsLoopbackOnly_rejects_other_addresses(string address) =>
        DevHostBinding.IsLoopbackOnly([address]).Should().BeFalse();

    [Fact]
    public void IsLoopbackOnly_rejects_a_list_with_one_exposed_address() =>
        DevHostBinding.IsLoopbackOnly(["http://127.0.0.1:5180", "http://0.0.0.0:5180"]).Should().BeFalse();

    [Fact]
    public void IsLoopbackOnly_rejects_an_empty_set() =>
        DevHostBinding.IsLoopbackOnly([]).Should().BeFalse();
}
