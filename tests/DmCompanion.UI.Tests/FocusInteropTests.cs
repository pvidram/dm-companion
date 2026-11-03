using DmCompanion.UI.Interop;
using Microsoft.AspNetCore.Components;

namespace DmCompanion.UI.Tests;

public class FocusInteropTests : BunitContext
{
    [Fact]
    public async Task EnterAsync_calls_enter_in_the_focus_module_with_the_trap_flag()
    {
        var module = JSInterop.SetupModule(FocusInterop.ModulePath);
        module.SetupVoid("enter", _ => true).SetVoidResult();
        await using var interop = new FocusInterop(JSInterop.JSRuntime);

        await interop.EnterAsync(new ElementReference("panel"), trap: true);

        module.VerifyInvoke("enter").Arguments[1].Should().Be(true);
    }

    [Fact]
    public async Task ExitAsync_calls_exit_in_the_focus_module()
    {
        var module = JSInterop.SetupModule(FocusInterop.ModulePath);
        module.SetupVoid("exit", _ => true).SetVoidResult();
        await using var interop = new FocusInterop(JSInterop.JSRuntime);

        await interop.ExitAsync(new ElementReference("panel"));

        module.VerifyInvoke("exit").Arguments.Should().HaveCount(1);
    }

    [Fact]
    public async Task A_failed_module_import_is_retried_on_the_next_call()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        await using var interop = new FocusInterop(JSInterop.JSRuntime);
        var first = async () => await interop.ExitAsync(new ElementReference("panel"));
        await first.Should().ThrowAsync<Exception>();

        var module = JSInterop.SetupModule(FocusInterop.ModulePath);
        module.SetupVoid("exit", _ => true).SetVoidResult();
        await interop.ExitAsync(new ElementReference("panel"));

        module.VerifyInvoke("exit");
    }
}
