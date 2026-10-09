namespace Loopsmith.Core.Tests.Architecture;

/// <summary>
/// Rules 1 and 2 (slice part): feature slices and <c>SourceFetching</c> depend only on the kernel and themselves;
/// the kernel depends on no slice (<c>Domain</c> → <c>Functional</c>, <c>Phrasing</c>/<c>Causality</c> → both).
/// </summary>
public sealed class SliceBoundaryTests
{
    public static TheoryData<string> KernelOnlySlices => Slices.ToTheoryData(Slices.KernelOnly);

    public static TheoryData<string> KernelSlices => Slices.ToTheoryData(Slices.Kernel);

    [Theory]
    [MemberData(nameof(KernelOnlySlices))]
    public void Feature_and_boundary_slices_depend_only_on_the_kernel_and_themselves(string slice) =>
        AssertDependsOnlyOnAllowedSlices(slice);

    [Theory]
    [MemberData(nameof(KernelSlices))]
    public void Kernel_modules_depend_on_no_slice(string slice) =>
        AssertDependsOnlyOnAllowedSlices(slice);

    [Fact]
    public void Every_core_type_lives_in_a_known_slice()
    {
        var strays =
            from type in CoreAssembly.Types
            where type.DeclaringType is null
            let inCoreNamespace = Slices.IsCoreNamespace(type.Namespace)
            where inCoreNamespace
                ? !Slices.All.Contains(Slices.FindSlice(type.Namespace) ?? "")
                : !CoreAssembly.IsToolGenerated(type)
            select $"{type.FullName} (namespace '{type.Namespace}')";

        Violations.AssertNone(
            $"Every Loopsmith.Core type must live in a known slice ({string.Join(", ", Slices.All)}). "
                + "Add a new slice to tests/Loopsmith.Core.Tests/Architecture/Slices.cs deliberately.",
            strays);
    }

    private static void AssertDependsOnlyOnAllowedSlices(string slice)
    {
        string[] allowed = [slice, .. Slices.AllowedDependencies[slice]];

        var violations = CoreAssembly.FindForbiddenDependencies(
            type => CoreAssembly.FindSlice(type) == slice,
            (_, dependency) => Slices.IsCoreNamespace(dependency.Namespace)
                && !allowed.Contains(Slices.FindSlice(dependency.Namespace) ?? ""));

        Violations.AssertNone(
            $"Slice '{slice}' may depend only on: {string.Join(", ", allowed)}.",
            violations);
    }
}
