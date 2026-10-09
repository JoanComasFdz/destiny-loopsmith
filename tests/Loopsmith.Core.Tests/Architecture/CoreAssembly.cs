using System.Collections.Concurrent;
using System.Collections.Immutable;
using Mono.Cecil;

namespace Loopsmith.Core.Tests.Architecture;

/// <summary>The compiled <c>Loopsmith.Core</c> assembly, read with Mono.Cecil (IL included).</summary>
internal static class CoreAssembly
{
    private const string CompilerGenerated = "System.Runtime.CompilerServices.CompilerGeneratedAttribute";
    private const string GeneratedCode = "System.CodeDom.Compiler.GeneratedCodeAttribute";
    private const string Embedded = "Microsoft.CodeAnalysis.EmbeddedAttribute";

    private static readonly Lazy<ModuleDefinition> LazyModule = new(ReadModule);
    private static readonly Lazy<ImmutableHashSet<string>> LazyLoadableAssemblies = new(FindLoadableAssemblies);
    private static readonly ConcurrentDictionary<TypeDefinition, ImmutableHashSet<TypeDependency>> DependencyCache = new();

    public static ModuleDefinition Module => LazyModule.Value;

    /// <summary>Every type in the assembly, nested and compiler-generated ones (closures, state machines) included.</summary>
    public static IEnumerable<TypeDefinition> Types => Module.GetTypes().Where(type => type.Name != "<Module>");

    /// <summary>Types whose outermost declaring type lives in <paramref name="slice"/> (empty if the slice has no types yet).</summary>
    public static IEnumerable<TypeDefinition> FindTypesInSlice(string slice) =>
        Types.Where(type => FindSlice(type) == slice);

    /// <summary>The slice a type belongs to, judged by its outermost declaring type (nested types have no namespace).</summary>
    public static string? FindSlice(TypeDefinition type) => Slices.FindSlice(FindOutermost(type).Namespace);

    public static TypeDefinition FindOutermost(TypeDefinition type)
    {
        var current = type;
        while (current.DeclaringType is not null)
        {
            current = current.DeclaringType;
        }

        return current;
    }

    /// <summary>Lambdas' closures, iterators and async state machines (or anything nested in them).</summary>
    public static bool IsCompilerGenerated(TypeDefinition type) =>
        EnumerateSelfAndDeclaringTypes(type).Any(t => HasAttribute(t, CompilerGenerated));

    /// <summary>
    /// Emitted by the compiler or a source generator (Vogen's factory, embedded attributes). Note that Dunet also
    /// stamps <c>[GeneratedCode]</c> on hand-written union types, so do not use this to exempt members from rules.
    /// </summary>
    public static bool IsToolGenerated(TypeDefinition type) =>
        EnumerateSelfAndDeclaringTypes(type).Any(t =>
            HasAttribute(t, CompilerGenerated) || HasAttribute(t, GeneratedCode) || HasAttribute(t, Embedded));

    public static bool HasAttribute(ICustomAttributeProvider provider, string attributeFullName) =>
        provider.CustomAttributes.Any(attribute => attribute.AttributeType.FullName == attributeFullName);

    public static bool HasCompilerGeneratedAttribute(ICustomAttributeProvider provider) =>
        HasAttribute(provider, CompilerGenerated);

    public static ImmutableHashSet<TypeDependency> CollectDependencies(TypeDefinition type) =>
        DependencyCache.GetOrAdd(type, t => TypeDependencies.CollectDependencies(t, CanDecodeArguments));

    /// <summary>
    /// Decoding attribute arguments may need the assemblies of their (enum) parameter types. Generator-only
    /// assemblies (e.g. <c>Vogen.SharedTypes</c>, a PrivateAssets reference) are not deployed with the tests, so
    /// such attributes contribute only their attribute type. <c>typeof</c> arguments of framework and Core
    /// attributes are still collected.
    /// </summary>
    public static bool CanDecodeArguments(CustomAttribute attribute) =>
        IsLoadable(attribute.Constructor.DeclaringType)
        && attribute.Constructor.Parameters.All(parameter => IsLoadable(parameter.ParameterType));

    /// <summary>
    /// "Owner -> dependency" for every type matching <paramref name="isOwnerChecked"/> that references a type matching
    /// <paramref name="isForbidden"/>. The owner is the outermost declaring type, so a lambda's dependency is
    /// reported against the type that wrote the lambda.
    /// </summary>
    public static IEnumerable<string> FindForbiddenDependencies(
        Func<TypeDefinition, bool> isOwnerChecked,
        Func<TypeDefinition, TypeDependency, bool> isForbidden) =>
        from type in Types
        where isOwnerChecked(type)
        from dependency in CollectDependencies(type)
        where isForbidden(type, dependency)
        select $"{FindOutermost(type).FullName} -> {dependency.FullName}";

    private static IEnumerable<TypeDefinition> EnumerateSelfAndDeclaringTypes(TypeDefinition type)
    {
        for (var current = type; current is not null; current = current.DeclaringType)
        {
            yield return current;
        }
    }

    private static bool IsLoadable(TypeReference type) =>
        type.GetElementType().Scope switch
        {
            ModuleDefinition => true,
            AssemblyNameReference assembly => LazyLoadableAssemblies.Value.Contains(assembly.Name),
            _ => false,
        };

    private static string FindCorePath() => typeof(Loopsmith.Core.Functional.Unit).Assembly.Location;

    /// <summary>Where referenced assemblies are looked up: next to the tests, and the shared framework.</summary>
    private static ImmutableArray<string> FindSearchDirectories() =>
    [
        Path.GetDirectoryName(FindCorePath())!,
        Path.GetDirectoryName(typeof(object).Assembly.Location)!,
    ];

    private static ImmutableHashSet<string> FindLoadableAssemblies() =>
    [
        .. from reference in Module.AssemblyReferences
           where FindSearchDirectories().Any(directory => File.Exists(Path.Combine(directory, reference.Name + ".dll")))
           select reference.Name,
    ];

    private static ModuleDefinition ReadModule()
    {
        var resolver = new DefaultAssemblyResolver();
        foreach (var directory in FindSearchDirectories())
        {
            resolver.AddSearchDirectory(directory);
        }

        return ModuleDefinition.ReadModule(FindCorePath(), new ReaderParameters { AssemblyResolver = resolver });
    }
}
