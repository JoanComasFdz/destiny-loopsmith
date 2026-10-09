using Mono.Cecil;

namespace Loopsmith.Core.Tests.Architecture;

/// <summary>
/// Rule 6: kernel types (Domain, Functional, Phrasing, Causality) are immutable — no public property setter other
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
    public void Kernel_types_are_immutable(string slice)
    {
        var types = CoreAssembly.FindTypesInSlice(slice)
            .Where(type => !CoreAssembly.IsCompilerGenerated(type))
            .ToList();

        var mutableProperties =
            from type in types
            from property in type.Properties
            where property.SetMethod is { IsPublic: true } setter && !IsInitOnly(setter)
            select $"{type.FullName}.{property.Name} (public set; use init)";

        var mutableFields =
            from type in types
            from field in type.Fields
            where field.IsPublic && !field.IsInitOnly && !field.IsLiteral && !field.IsRuntimeSpecialName   // enum value__
            select $"{type.FullName}.{field.Name} (public writable field)";

        Violations.AssertNone(
            $"Types in kernel module '{slice}' must be immutable.",
            mutableProperties.Concat(mutableFields));
    }

    private static bool IsInitOnly(MethodDefinition setter) =>
        setter.ReturnType is RequiredModifierType { ModifierType.FullName: IsExternalInit };
}
