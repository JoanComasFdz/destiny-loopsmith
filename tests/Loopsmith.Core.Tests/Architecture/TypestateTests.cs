using Loopsmith.Core.Domain;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Loopsmith.Core.Tests.Architecture;

/// <summary>
/// Rule 5: only <c>BuildComposition</c> constructs <see cref="ValidatedBuild"/> (typestate: "simulate before
/// validation" cannot happen). Scans the IL of every Core method for <c>newobj</c> of its constructors and calls
/// to its compiler-generated <c>&lt;Clone&gt;$</c> (what a <c>with</c> expression compiles to).
/// </summary>
public sealed class TypestateTests
{
    private const string CloneMethod = "<Clone>$";

    [Fact]
    public void Only_BuildComposition_constructs_ValidatedBuild()
    {
        var validatedBuild = typeof(ValidatedBuild).FullName!;
        Assert.NotNull(CoreAssembly.Module.GetType(validatedBuild));

        var violations =
            from type in CoreAssembly.Types
            where CoreAssembly.FindSlice(type) != Slices.BuildComposition
            from method in type.Methods
            where method.HasBody && !IsRecordPlumbingOf(method, validatedBuild)
            from instruction in method.Body.Instructions
            let construction = DescribeConstruction(instruction, validatedBuild)
            where construction is not null
            select $"{type.FullName}::{method.Name} ({construction})";

        Violations.AssertNone(
            $"Only '{Slices.BuildComposition}' may construct {validatedBuild} (new or with).",
            violations);
    }

    /// <summary>The record's own compiler-generated members (its <c>&lt;Clone&gt;$</c> calls the copy constructor).</summary>
    private static bool IsRecordPlumbingOf(MethodDefinition method, string recordFullName) =>
        method.DeclaringType.FullName == recordFullName && CoreAssembly.HasCompilerGeneratedAttribute(method);

    private static string? DescribeConstruction(Instruction instruction, string recordFullName) =>
        instruction.Operand is MethodReference called && called.DeclaringType.FullName == recordFullName
            ? (instruction.OpCode.Code, called.Name) switch
            {
                (Code.Newobj, ".ctor") => "new ValidatedBuild",
                (Code.Call or Code.Callvirt, CloneMethod) => "with on ValidatedBuild",
                _ => null,
            }
            : null;
}
