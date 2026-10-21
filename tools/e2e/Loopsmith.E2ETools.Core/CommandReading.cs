using System.Collections.Immutable;
using Dunet;
using Loopsmith.Functional;

namespace Loopsmith.E2ETools.Core;

/// <summary>
/// What the tool is asked to do: <c>post</c> (the E2E videos workflow) or one of the steps the e2e scripts call
/// (tools/e2e/record.sh, tools/e2e/push-gifs.sh, .devcontainer/6-install-e2e-recordings-extension.sh).
/// </summary>
[Union]
public partial record ToolCommand
{
    /// <summary>Post a PR's recorded scenarios, or clean up after it closed (<see cref="OptionsReading"/>).</summary>
    partial record Post(E2EToolsOptions Options);

    /// <summary>A port free on 127.0.0.1, for a server the script starts next.</summary>
    partial record FindFreePort();

    /// <summary>The VS Code build a VS Code for the web runner's package.json pins (its <c>config.vscodeCommit</c>).</summary>
    partial record ReadVsCodeCommit(string PackageJson);

    /// <summary>Each unrecorded run's result and time, from the test results (TRX files).</summary>
    partial record ReportRuns(ImmutableArray<string> TestResults);

    /// <summary>A recorded run's domain folder, GIF name, screen and the second its video shows Home.</summary>
    partial record FindHome(string Record);

    /// <summary>Each rule the recorded runs' Gherkins name, once per domain folder.</summary>
    partial record ListFlows(ImmutableArray<string> Records);

    /// <summary>The recording's <c>scenarios.json</c>, written in <see cref="Out"/> from its runs' records.</summary>
    partial record WriteScenarios(string Out, string Selection, ImmutableArray<string> Records);

    /// <summary>The files a <c>scenarios.json</c> lists: each run's GIF and MP4, each rule's flow once.</summary>
    partial record ListFiles(string Scenarios);

    /// <summary>The VS Code extension in <see cref="Source"/> packed as a .vsix.</summary>
    partial record PackVsix(string Source, string License, string Vsix);
}

/// <summary>The command line → the command, or what is wrong with it.</summary>
public static class CommandReading
{
    public const string Usage = """
        usage: dotnet run --project tools/e2e/Loopsmith.E2ETools -- <command>
          post --pr <n> (--commit <sha> [--out <dir>] | --closed [--merged]) [--repo <owner/repo>] [--dry-run]
          free-port
          vscode-commit <package.json>
          report-runs <test results .trx>...
          find-home <run record .json>
          list-flows <run record .json>...
          write-scenarios <out dir> <selection .tsv> <run record .json>...
          list-files <scenarios.json>
          pack-vsix <extension folder> <LICENSE> <.vsix>
        """;

    public static Result<ToolCommand, string> ReadCommand(IReadOnlyList<string> args, Optional<string> environmentRepo) =>
        args.ToArray() switch
        {
            ["post", ..] => OptionsReading.ReadOptions(args, environmentRepo).Map(options => (ToolCommand)new ToolCommand.Post(options)),
            ["free-port"] => Read(new ToolCommand.FindFreePort()),
            ["vscode-commit", var packageJson] => Read(new ToolCommand.ReadVsCodeCommit(packageJson)),
            ["report-runs", .. var results] when results.Length > 0 => Read(new ToolCommand.ReportRuns([.. results])),
            ["find-home", var record] => Read(new ToolCommand.FindHome(record)),
            ["list-flows", .. var records] => Read(new ToolCommand.ListFlows([.. records])),
            ["write-scenarios", var outFolder, var selection, .. var records] => Read(new ToolCommand.WriteScenarios(outFolder, selection, [.. records])),
            ["list-files", var scenarios] => Read(new ToolCommand.ListFiles(scenarios)),
            ["pack-vsix", var source, var license, var vsix] => Read(new ToolCommand.PackVsix(source, license, vsix)),
            [var command, ..] when IsCommand(command) => new Result<ToolCommand, string>.Error($"'{command}' takes other arguments.\n{Usage}"),
            [var command, ..] => new Result<ToolCommand, string>.Error($"Unknown command '{command}'.\n{Usage}"),
            _ => new Result<ToolCommand, string>.Error($"No command.\n{Usage}"),
        };

    private static Result<ToolCommand, string> Read(ToolCommand command) => new Result<ToolCommand, string>.Ok(command);

    private static bool IsCommand(string command) =>
        command is "free-port" or "vscode-commit" or "report-runs" or "find-home" or "list-flows" or "write-scenarios" or "list-files" or "pack-vsix";
}
