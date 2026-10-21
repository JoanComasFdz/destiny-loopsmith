using Loopsmith.E2ETools.Core;
using Loopsmith.Functional;

namespace Loopsmith.E2ETools.Tests;

/// <summary>The command line the e2e scripts and the E2E videos workflow give the tool.</summary>
public sealed class CommandReadingTests
{
    private static readonly Optional<string> Environment = Optional.Some("Owner/repo");

    [Fact]
    public void Post_reads_its_options_as_before()
    {
        var command = Unwrap(CommandReading.ReadCommand(["post", "--pr", "7", "--commit", "abc"], Environment));

        Assert.Equal(new E2EToolsOptions(7, "Owner/repo", new E2ECommand.PostScenarios("abc", "out"), DryRun: false), Assert.IsType<ToolCommand.Post>(command).Options);
    }

    [Fact]
    public void Each_script_step_takes_its_files()
    {
        Assert.IsType<ToolCommand.FindFreePort>(Unwrap(CommandReading.ReadCommand(["free-port"], Environment)));
        Assert.Equal(new ToolCommand.ReadVsCodeCommit("package.json"), Unwrap(CommandReading.ReadCommand(["vscode-commit", "package.json"], Environment)));
        Assert.Equal(["web.trx", "vscode.trx"], Assert.IsType<ToolCommand.ReportRuns>(Unwrap(CommandReading.ReadCommand(["report-runs", "web.trx", "vscode.trx"], Environment))).TestResults);
        Assert.Equal(new ToolCommand.FindHome("a.json"), Unwrap(CommandReading.ReadCommand(["find-home", "a.json"], Environment)));
        Assert.Empty(Assert.IsType<ToolCommand.ListFlows>(Unwrap(CommandReading.ReadCommand(["list-flows"], Environment))).Records);
        var write = Assert.IsType<ToolCommand.WriteScenarios>(Unwrap(CommandReading.ReadCommand(["write-scenarios", "out", "sel.tsv", "a.json", "b.json"], Environment)));
        Assert.Equal(("out", "sel.tsv"), (write.Out, write.Selection));
        Assert.Equal(["a.json", "b.json"], write.Records);
        Assert.Equal(new ToolCommand.ListFiles("out/scenarios.json"), Unwrap(CommandReading.ReadCommand(["list-files", "out/scenarios.json"], Environment)));
        Assert.Equal(new ToolCommand.PackVsix("ext", "LICENSE", "x.vsix"), Unwrap(CommandReading.ReadCommand(["pack-vsix", "ext", "LICENSE", "x.vsix"], Environment)));
    }

    [Theory]
    [InlineData(new string[0], "No command.")]
    [InlineData(new[] { "draw" }, "Unknown command 'draw'.")]
    [InlineData(new[] { "report-runs" }, "'report-runs' takes other arguments.")]
    [InlineData(new[] { "pack-vsix", "ext" }, "'pack-vsix' takes other arguments.")]
    public void A_wrong_command_line_says_what_is_wrong_and_shows_the_usage(string[] args, string error)
    {
        var failure = Assert.IsType<Result<ToolCommand, string>.Error>(CommandReading.ReadCommand(args, Environment)).Failure;

        Assert.StartsWith(error, failure);
        Assert.Contains("write-scenarios <out dir>", failure);
    }

    [Fact]
    public void The_static_server_takes_a_folder_and_a_port_that_is_0_by_default()
    {
        Assert.Equal(new ServerOptions("site", 0), Assert.IsType<Result<ServerOptions, string>.Ok>(ServerOptionsReading.ReadServerOptions(["site"])).Value);
        Assert.Equal(new ServerOptions("site", 8080), Assert.IsType<Result<ServerOptions, string>.Ok>(ServerOptionsReading.ReadServerOptions(["site", "--port", "8080"])).Value);
        Assert.IsType<Result<ServerOptions, string>.Error>(ServerOptionsReading.ReadServerOptions(["site", "--port", "web"]));
        Assert.IsType<Result<ServerOptions, string>.Error>(ServerOptionsReading.ReadServerOptions([]));
        Assert.Equal("http://127.0.0.1:5000/", ServerOptionsReading.WriteListeningLine("http://127.0.0.1:5000"));
    }

    private static ToolCommand Unwrap(Result<ToolCommand, string> result) => Assert.IsType<Result<ToolCommand, string>.Ok>(result).Value;
}
