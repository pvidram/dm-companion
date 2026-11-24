using System.Runtime.CompilerServices;
using Bunit;

namespace DmCompanion.Screens.Tests;

internal static class TestAssemblySetup
{
    /// <summary>
    /// bUnit's default <c>WaitFor*</c> budget is 1 s. Many tests wait on handlers that do real file I/O (creating a note, loading
    /// a scene) and, with several test collections running in parallel, the dispatcher can take longer than that to run even the
    /// first check (bUnit reports "Check count: 0"). The explicit waits in the tests already use 10 s; this aligns the rest.
    /// <c>DefaultWaitTimeout</c> is a process-global static; that is fine in this test-only assembly.
    /// It is only an upper bound: a passing wait still returns as soon as its condition holds.
    /// </summary>
    [ModuleInitializer]
    internal static void RaiseDefaultWaitTimeout() => BunitContext.DefaultWaitTimeout = TimeSpan.FromSeconds(10);
}
