namespace Loopsmith.Core.Tests.Architecture;

/// <summary>
/// Rule 7: Core declares no interfaces — the domain is modelled with Dunet unions ("never ISomething + two
/// classes") and behaviour is reached through static direct calls (CONVENTIONS.md). Interfaces emitted by the
/// compiler or a source generator (Vogen, Dunet) are exempt.
/// </summary>
public sealed class NoInterfaceTests
{
    [Fact]
    public void Core_declares_no_interfaces()
    {
        var interfaces =
            from type in CoreAssembly.Types
            where type.IsInterface && !CoreAssembly.IsToolGenerated(type)
            select type.FullName;

        Violations.AssertNone(
            "Loopsmith.Core must not declare interfaces: use a Dunet union for data and static functions for behaviour.",
            interfaces);
    }
}
