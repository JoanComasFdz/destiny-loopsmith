using Mono.Cecil;

namespace Loopsmith.Core.Tests.Architecture;

/// <summary>
/// Rule 6: kernel types (Domain, Phrasing, Causality) are immutable (Loopsmith.Functional's are checked by
/// <see cref="FunctionalProjectTests"/> with <see cref="FindMutableMembers"/>) — no public property setter other
/// than <c>init</c> (an init accessor's return type carries <c>modreq(IsExternalInit)</c>) and no public writable
/// field. Compiler-generated closures/state machines are skipped (their public fields are captured locals).
/// Dunet and Vogen currently generate only init-only/get-only members, so they need no exemption.
/// </summary>
public sealed class ImmutabilityTests
{
    private const string IsExternalInit = "System.Runtime.CompilerServices.IsExternalInit";

    public static TheoryData<string> KernelSlices => Slices.ToTheoryData(Slices.Kernel);

    [Theory]
    [MemberData(nameof(KernelSlices))]
    public void Kernel_types_are_immutable(string slice) =>
        Violations.AssertNone(
            $"Types in kernel module '{slice}' must be immutable.",
            FindMutableMembers(CoreAssembly.FindTypesInSlice(slice)));

    /// <summary>Each public setter (not <c>init</c>) and public writable field of <paramref name="types"/>, closures skipped.</summary>
    public static IEnumerable<string> FindMutableMembers(IEnumerable<TypeDefinition> types)
    {
        var checkedTypes = types.Where(type => !CoreAssembly.IsCompilerGenerated(type)).ToList();

        var mutableProperties =
            from type in checkedTypes
            from property in type.Properties
            where property.SetMethod is { IsPublic: true } setter && !IsInitOnly(setter)
            select $"{type.FullName}.{property.Name} (public set; use init)";

        var mutableFields =
            from type in checkedTypes
            from field in type.Fields
            where field.IsPublic && !field.IsInitOnly && !field.IsLiteral && !field.IsRuntimeSpecialName   // enum value__
            select $"{type.FullName}.{field.Name} (public writable field)";

        return mutableProperties.Concat(mutableFields);
    }

    private static bool IsInitOnly(MethodDefinition setter) =>
        setter.ReturnType is RequiredModifierType { ModifierType.FullName: IsExternalInit };
}
