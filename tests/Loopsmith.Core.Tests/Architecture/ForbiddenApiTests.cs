namespace Loopsmith.Core.Tests.Architecture;

/// <summary>
/// Rules 2 (API part), 3 and 4: I/O stays in <c>SourceFetching</c>, the console in the CLI host,
/// YamlDotNet in the parsing slices, and the kernel touches none of them.
/// </summary>
public sealed class ForbiddenApiTests
{
    private const string SystemIO = "System.IO";
    private const string SystemNet = "System.Net";
    private const string SystemNetHttp = "System.Net.Http";
    private const string SystemConsole = "System.Console";
    private const string YamlDotNet = "YamlDotNet";

    /// <summary>
    /// In-memory text readers do no I/O; YamlDotNet's <c>Parser</c>/<c>YamlStream.Load</c> only accept a
    /// <c>TextReader</c>, so the YAML parsing slices may wrap a string in a <c>StringReader</c>.
    /// </summary>
    private static readonly string[] InMemoryTextReaders = ["System.IO.StringReader", "System.IO.TextReader"];

    /// <summary>
    /// In-memory text writers do no I/O either; YamlDotNet's <c>Emitter</c> only writes to a <c>TextWriter</c>, so the
    /// YAML writing slice may collect its output in a <c>StringWriter</c>.
    /// </summary>
    private static readonly string[] InMemoryTextWriters = ["System.IO.StringWriter", "System.IO.TextWriter"];

    public static TheoryData<string> KernelSlices => Slices.ToTheoryData(Slices.Kernel);

    [Theory]
    [MemberData(nameof(KernelSlices))]
    public void Kernel_uses_no_IO_network_console_or_YamlDotNet(string slice)
    {
        var violations = CoreAssembly.FindForbiddenDependencies(
            type => CoreAssembly.FindSlice(type) == slice,
            (_, dependency) => dependency.IsIn(SystemIO)
                || dependency.IsIn(SystemNet)
                || dependency.FullName == SystemConsole
                || dependency.IsIn(YamlDotNet));

        Violations.AssertNone(
            $"Kernel module '{slice}' must not use {SystemIO}, {SystemNet}, {SystemConsole} or {YamlDotNet}.",
            violations);
    }

    [Fact]
    public void Only_SourceFetching_uses_file_or_network_IO()
    {
        var violations = CoreAssembly.FindForbiddenDependencies(
            type => CoreAssembly.FindSlice(type) != Slices.SourceFetching,
            (type, dependency) => dependency.IsIn(SystemNetHttp)
                || (dependency.IsIn(SystemIO) && !IsAllowedInMemoryReader(type, dependency)));

        Violations.AssertNone(
            $"Only '{Slices.SourceFetching}' may use {SystemIO} or {SystemNetHttp} "
                + $"(the YAML parsing slices may use {string.Join("/", InMemoryTextReaders)}, "
                + $"the YAML writing slice {string.Join("/", InMemoryTextWriters)}).",
            violations);
    }

    [Fact]
    public void No_core_type_uses_the_console()
    {
        var violations = CoreAssembly.FindForbiddenDependencies(
            _ => true,
            (_, dependency) => dependency.FullName == SystemConsole);

        Violations.AssertNone($"No Loopsmith.Core type may use {SystemConsole}: console I/O belongs to the CLI host.", violations);
    }

    [Fact]
    public void Only_the_parsing_slices_use_YamlDotNet()
    {
        var violations = CoreAssembly.FindForbiddenDependencies(
            type => !Slices.YamlParsers.Contains(CoreAssembly.FindSlice(type) ?? ""),
            (_, dependency) => dependency.IsIn(YamlDotNet));

        Violations.AssertNone($"Only {string.Join(" and ", Slices.YamlParsers)} may use {YamlDotNet}.", violations);
    }

    private static bool IsAllowedInMemoryReader(Mono.Cecil.TypeDefinition type, TypeDependency dependency) =>
        (Slices.YamlParsers.Contains(CoreAssembly.FindSlice(type) ?? "") && InMemoryTextReaders.Contains(dependency.FullName))
        || (Slices.YamlWriters.Contains(CoreAssembly.FindSlice(type) ?? "") && InMemoryTextWriters.Contains(dependency.FullName));
}
