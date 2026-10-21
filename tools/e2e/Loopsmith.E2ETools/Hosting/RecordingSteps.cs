using System.Collections.Immutable;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using Loopsmith.E2ETools.Core;
using Loopsmith.Functional;

namespace Loopsmith.E2ETools.Hosting;

/// <summary>
/// The steps the e2e scripts call (tools/e2e/README.md): record.sh's free port, VS Code build, unrecorded runs' report,
/// Home in a video, rules' flows and scenarios.json; push-gifs.sh's files to push; and the dev container's .vsix. Each
/// reads its files, calls the pure logic and prints what the script reads.
/// </summary>
public static class RecordingSteps
{
    /// <summary>A port free on 127.0.0.1 now (the system's pick; the script's server takes it next).</summary>
    public static Result<Unit, string> FindFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);   // impure
        listener.Start();   // impure
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;   // impure
        var line = port.ToString(CultureInfo.InvariantCulture);   // pure
        return Report(line);   // impure
    }

    public static Result<Unit, string> ReadVsCodeCommit(ToolCommand.ReadVsCodeCommit command)
    {
        var json = E2EFiles.ReadFile(command.PackageJson);   // impure
        var commit = json.Bind(JsonReading.ReadVsCodeCommit).MapError(failure => $"{command.PackageJson}: {failure}");   // pure
        return commit.Bind(Report);   // impure
    }

    /// <summary>The unrecorded runs' table; an error when a results file is missing or no run passed or failed.</summary>
    public static Result<Unit, string> ReportRuns(ToolCommand.ReportRuns command)
    {
        var missing = command.TestResults.Where(path => !File.Exists(path)).ToImmutableArray();   // impure
        if (!missing.IsEmpty)
        {
            return new Result<Unit, string>.Error($"No test results were written ({Path.GetFileName(missing[0])}).");
        }

        var texts = command.TestResults.Select(path => (Path: path, Text: E2EFiles.ReadFile(path))).ToImmutableArray();   // impure
        var runs = texts.Select(file => file.Text.Bind(trx => RunReporting.ReadTestRuns(trx, file.Path))).CombineAll()
            .MapError(failures => failures[0]).Map(each => each.SelectMany(fileRuns => fileRuns).ToImmutableArray());   // pure
        if (runs is not Result<ImmutableArray<TestRun>, string>.Ok all)
        {
            return new Result<Unit, string>.Error(((Result<ImmutableArray<TestRun>, string>.Error)runs).Failure);
        }

        var report = RunReporting.WriteReport(all.Value);   // pure
        E2EFiles.ReportAll(report);   // impure
        return RunReporting.HasRunAny(all.Value)
            ? new Result<Unit, string>.Ok(new Unit.Value())
            : new Result<Unit, string>.Error("No scenario ran.");
    }

    /// <summary>A recorded run's "&lt;domain&gt; &lt;gif&gt; &lt;screen&gt; &lt;second Home shows in its video&gt;".</summary>
    public static Result<Unit, string> FindHome(ToolCommand.FindHome command)
    {
        var json = E2EFiles.ReadFile(command.Record);   // impure
        var read = json.Bind(JsonReading.ReadRunRecord).MapError(failure => $"{command.Record}: {failure}");   // pure
        if (read is not Result<RunRecord, string>.Ok record)
        {
            return new Result<Unit, string>.Error(((Result<RunRecord, string>.Error)read).Failure);
        }

        var noted = record.Value.HomeShownSeconds.UnwrapOr(0);   // pure
        var screenshot = HomeFinding.FindScreenshot(command.Record);   // pure
        var screenshotTaken = File.Exists(screenshot);   // impure
        if (!screenshotTaken)
        {
            var notedLine = HomeFinding.WriteHomeLine(record.Value, noted);   // pure
            return Report(notedLine);   // impure
        }

        var probe = HomeFinding.WriteProbeArguments(HomeFinding.FindVideo(command.Record), screenshot);   // pure
        var answer = ChildProcesses.RunCommand("ffmpeg", probe, Optional.None<string>());   // impure
        if (answer.ExitCode != 0)
        {
            return new Result<Unit, string>.Error($"ffmpeg couldn't read {HomeFinding.FindVideo(command.Record)}:\n{answer.Errors.Trim()}");
        }

        var home = HomeFinding.FindHome(noted, answer.Errors);   // pure
        var line = HomeFinding.WriteHomeLine(record.Value, home);   // pure
        return Report(line);   // impure
    }

    /// <summary>Each rule to draw, "&lt;domain&gt; &lt;rule&gt;" a line.</summary>
    public static Result<Unit, string> ListFlows(ToolCommand.ListFlows command)
    {
        var texts = ReadTexts(command.Records);   // impure
        var lines = ReadRecords(texts).Map(records => ScenarioListing.ListFlows(records).Select(ScenarioListing.WriteFlowLine).ToImmutableArray());   // pure
        return lines.Bind(ReportAll);   // impure
    }

    /// <summary>
    /// <c>&lt;out&gt;/scenarios.json</c> from the runs' records, the selection and the flows drawn (each rule's PNG in its
    /// domain folder, or its error in <c>&lt;out&gt;/.flow-errors/&lt;rule&gt;.txt</c>), then the summary.
    /// </summary>
    public static Result<Unit, string> WriteScenarios(ToolCommand.WriteScenarios command)
    {
        var texts = ReadTexts(command.Records);   // impure
        var read = ReadRecords(texts);   // pure
        if (read is not Result<ImmutableArray<RunRecord>, string>.Ok records)
        {
            return new Result<Unit, string>.Error(((Result<ImmutableArray<RunRecord>, string>.Error)read).Failure);
        }

        var tsv = E2EFiles.ReadFile(command.Selection);   // impure
        if (tsv is not Result<string, string>.Ok selectionText)
        {
            return new Result<Unit, string>.Error(((Result<string, string>.Error)tsv).Failure);
        }

        var selection = ScenarioListing.ReadSelection(selectionText.Value);   // pure
        var paths = ScenarioListing.PlanFlowFiles(ScenarioListing.ListFlows(records.Value));   // pure
        var found = E2EFiles.FindFlowFiles(command.Out, paths);   // impure
        var json = ScenarioListing.WriteScenarios(records.Value, selection, ScenarioListing.ReadDrawnFlows(found));   // pure
        var summary = ScenarioListing.WriteSummary(records.Value, selection);   // pure
        var written = E2EFiles.WriteFile(Path.Combine(command.Out, "scenarios.json"), json);   // impure
        return written.Bind(_ => ReportAll(summary));   // impure
    }

    /// <summary>The files a scenarios.json lists, a path a line.</summary>
    public static Result<Unit, string> ListFiles(ToolCommand.ListFiles command)
    {
        var json = E2EFiles.ReadFile(command.Scenarios);   // impure
        var files = json.Bind(JsonReading.ReadListedFiles).MapError(failure => $"{command.Scenarios}: {failure}");   // pure
        return files.Bind(ReportAll);   // impure
    }

    /// <summary>The extension packed as a .vsix (deflated), replacing one already there.</summary>
    public static Result<Unit, string> PackVsix(ToolCommand.PackVsix command)
    {
        var entries = VsixPacking.PlanEntries(command.Source, command.License);   // pure
        try
        {
            File.Delete(command.Vsix);   // impure
            using var package = ZipFile.Open(command.Vsix, ZipArchiveMode.Create);   // impure
            foreach (var entry in entries)
            {
                package.CreateEntryFromFile(entry.From, entry.Name, CompressionLevel.Optimal);   // impure
            }

            return new Result<Unit, string>.Ok(new Unit.Value());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new Result<Unit, string>.Error($"Cannot pack {command.Vsix}: {exception.Message}");
        }
    }

    private static ImmutableArray<(string Path, Result<string, string> Text)> ReadTexts(ImmutableArray<string> paths) =>
        [.. paths.Select(path => (path, E2EFiles.ReadFile(path)))];

    private static Result<ImmutableArray<RunRecord>, string> ReadRecords(ImmutableArray<(string Path, Result<string, string> Text)> texts) =>
        texts.Select(file => file.Text.Bind(JsonReading.ReadRunRecord).MapError(failure => $"{file.Path}: {failure}"))
            .CombineAll()
            .MapError(failures => failures[0]);

    private static Result<Unit, string> Report(string line)
    {
        E2EFiles.Report(line);
        return new Result<Unit, string>.Ok(new Unit.Value());
    }

    private static Result<Unit, string> ReportAll(ImmutableArray<string> lines)
    {
        E2EFiles.ReportAll(lines);
        return new Result<Unit, string>.Ok(new Unit.Value());
    }
}
