using Mono.Cecil;

namespace Loopsmith.Core.Tests.Architecture;

/// <summary>
/// <c>Loopsmith.Functional</c> (Optional, Result, Unit) is a project of its own so a developer tool under <c>tools/</c> can
/// use the coding building blocks without building Core (docs/CODING_RULES.md, "The host owns no logic"). It keeps the
/// kernel's rules: it references nothing but .NET and Dunet, touches no I/O, network or console, its types live in
/// <c>Loopsmith.Functional</c> and are immutable. Core declaring a type there is caught by
/// <see cref="SliceBoundaryTests.Every_core_type_lives_in_a_known_slice"/>.
/// </summary>
public sealed class FunctionalProjectTests
{
    private const string Namespace = "Loopsmith.Functional";

    private static readonly Lazy<ModuleDefinition> LazyModule =
        new(() => ModuleDefinition.ReadModule(typeof(Loopsmith.Functional.Unit).Assembly.Location));

    private static IEnumerable<TypeDefinition> Types => LazyModule.Value.GetTypes().Where(type => type.Name != "<Module>");

    [Fact]
    public void Functional_references_only_dotnet_and_Dunet() =>
        Violations.AssertNone(
            $"{Namespace} may reference only .NET (System.*) and Dunet, so a tool can build it without Core.",
            from reference in LazyModule.Value.AssemblyReferences
            where !IsDotnetOrDunet(reference.Name)
            select reference.Name);

    [Fact]
    public void Functional_uses_no_IO_network_or_console() =>
        Violations.AssertNone(
            $"{Namespace} must not use System.IO, System.Net or System.Console.",
            from type in Types
            from dependency in TypeDependencies.CollectDependencies(type, _ => false)
            where dependency.IsIn("System.IO") || dependency.IsIn("System.Net") || dependency.FullName == "System.Console"
            select $"{CoreAssembly.FindOutermost(type).FullName} -> {dependency.FullName}");

    [Fact]
    public void Functional_types_live_in_its_namespace() =>
        Violations.AssertNone(
            $"Every type of the {Namespace} project must live in the {Namespace} namespace.",
            from type in Types
            where type.DeclaringType is null && !CoreAssembly.IsToolGenerated(type)
            where type.Namespace != Namespace && !type.Namespace.StartsWith(Namespace + ".", StringComparison.Ordinal)
            select $"{type.FullName} (namespace '{type.Namespace}')");

    [Fact]
    public void Functional_types_are_immutable() =>
        Violations.AssertNone($"Types in {Namespace} must be immutable.", ImmutabilityTests.FindMutableMembers(Types));

    private static bool IsDotnetOrDunet(string assembly) =>
        assembly is "System" or "netstandard" or "Dunet"
        || assembly.StartsWith("System.", StringComparison.Ordinal)
        || assembly.StartsWith("Dunet.", StringComparison.Ordinal);
}
