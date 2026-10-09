using Mono.Cecil;

namespace Loopsmith.Core.Tests.Architecture;

/// <summary>
/// CONVENTIONS "The host owns no logic": the CLI and the web designer call the <c>Orchestration</c> API and the
/// kernel, never a feature slice's functions directly. They may still use a slice's data types (building a
/// <c>TraceOptions</c>, reading a <c>LoopReport</c>): only calls to static methods — the slices' functions — count.
/// </summary>
public sealed class HostBoundaryTests
{
    /// <summary>Project folder and assembly name of each host (the CLI's assembly is <c>loopsmith</c>).</summary>
    public static TheoryData<string, string> Hosts => new() { { "Loopsmith.Cli", "loopsmith" }, { "Loopsmith.Web", "Loopsmith.Web" } };

    [Theory]
    [MemberData(nameof(Hosts))]
    public void Hosts_call_only_Orchestration_and_the_kernel(string host, string assembly)
    {
        var module = HostAssemblies.ReadModule(host, assembly);

        var violations =
            from type in module.GetTypes()
            from method in type.Methods
            where method.HasBody
            from instruction in method.Body.Instructions
            let called = instruction.Operand as MethodReference
            where called is not null && !called.HasThis && IsFeatureSliceFunction(called)
            select $"{CoreAssembly.FindOutermost(type).FullName} -> {called.DeclaringType.GetElementType().FullName}.{called.Name}";

        Violations.AssertNone(
            $"{host} may call only {Slices.Orchestration} and the kernel ({string.Join(", ", Slices.Kernel)}), "
                + "not a feature slice: add an Orchestration function (e.g. LoopDesigning) and call that.",
            violations);
    }

    /// <summary>A static method of a feature slice; operators (a record's <c>==</c>) belong to its data, not its functions.</summary>
    private static bool IsFeatureSliceFunction(MethodReference called)
    {
        var slice = Slices.FindSlice(FindOutermost(called.DeclaringType.GetElementType()).Namespace);
        return slice is not null
            && slice != Slices.Orchestration
            && !Slices.Kernel.Contains(slice)
            && !called.Name.StartsWith("op_", StringComparison.Ordinal);
    }

    private static TypeReference FindOutermost(TypeReference type)
    {
        var current = type;
        while (current.DeclaringType is not null)
        {
            current = current.DeclaringType;
        }

        return current;
    }
}

/// <summary>The built host assemblies (same configuration as the tests), read with Mono.Cecil.</summary>
internal static class HostAssemblies
{
    public static ModuleDefinition ReadModule(string host, string assembly)
    {
        var testsBin = new DirectoryInfo(AppContext.BaseDirectory);
        var framework = testsBin.Name;
        var configuration = testsBin.Parent!.Name;
        var path = Path.Combine(Support.RepoFiles.Root, "src", host, "bin", configuration, framework, $"{assembly}.dll");
        Assert.True(File.Exists(path), $"{path} not found: build the solution (the test project builds the hosts first).");
        return ModuleDefinition.ReadModule(path);
    }
}
