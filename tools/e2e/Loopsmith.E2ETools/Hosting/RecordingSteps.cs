using System.Collections.Immutable;
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
        E2EFiles.Report(((IPEndPoint)listener.LocalEndpoint).Port.ToString(System.Globalization.CultureInfo.InvariantCulture));   // impure
        return new Result<Unit, string>.Ok(new Unit.Value());
    }

    public static Result<Unit, string> ReadVsCodeCommit(ToolCommand.ReadVsCodeCommit command)
    {
        var json = E2EFiles.ReadFile(command.PackageJson);   // impure
        var commit = json.Bind(JsonReading.ReadVsCodeCommit).MapError(failure => $"{command.PackageJson}: {failure}");   // pure
        return commit.Map(Report);   // impure
    }

    /// <summary>The unrecorded runs' table; an error when a results file is missing or no run passed or failed.</summary>
    public static Result<Unit, string> ReportRuns(ToolCommand.ReportRuns command)
    {
        var missing = command.TestResults.Where(path => !File.Exists(path)).ToImmutableArray();   // impure
        if (!missing.IsEmpty)
        {
            return new Result<Unit, string>.Error($"No test results were written ({Path.GetFileName(missing[0])}).");
        }

        var read = command.TestResults.Select(path => E2EFiles.ReadFile(path).Bind(trx => RunReporting.ReadTestRuns(trx, path))).ToImmutableArray();   // impure
        var runs = read.CombineAll().MapError(failures => failures[0]).Map(each => each.SelectMany(fileRuns => fileRuns).ToImmutableArray());   // pure
        if (runs is not Result<ImmutableArray<TestRun>, string>.Ok all)
        {
            return new Result<Unit, string>.Error(((Result<ImmutableArray<TestRun>, string>.Error)runs).Failure);
        }

        E2EFiles.ReportAll(RunReporting.WriteReport(all.Value));   // impure
        return RunReporting.HasRunAny(all.Value)
            ? new Result<Unit, string>.Ok(new Unit.Value())
            : new Result<Unit, string>.Error("No scenario ran.");
    }

    /// <summary>A recorded run's "&lt;domain&gt; &lt;gif&gt; &lt;screen&gt; &lt;second Home shows in its video&gt;".</summary>
    public static Result<Unit, string> FindHome(ToolCommand.FindHome command)
    {
        var read = E2EFiles.ReadFile(command.Record).Bind(JsonReading.ReadRunRecord).MapError(failure => $"{command.Record}: {failure}");   // impure
        if (read is not Result<RunRecord, string>.Ok record)
        {
            return new Result<Unit, string>.Error(((Result<RunRecord, string>.Error)read).Failure);
        }

        var noted = record.Value.HomeShownSeconds.UnwrapOr(0);   // pure
        var probe = HomeFinding.WriteProbeArguments(HomeFinding.FindVideo(command.Record), HomeFinding.FindProbeStart(noted));   // pure
        var answer = ChildProcesses.RunCommand("ffmpeg", probe, Optional.None<string>());   // impure
        var home = HomeFinding.FindHome(noted, answer.Errors);   // pure
        E2EFiles.Report(HomeFinding.WriteHomeLine(record.Value, home));   // impure
        return new Result<Unit, string>.Ok(new Unit.Value());
    }

    /// <summary>Each rule to draw, "&lt;domain&gt; &lt;rule&gt;" a line.</summary>
    public static Result<Unit, string> ListFlows(ToolCommand.ListFlows command)
    {
        var records = ReadRecords(command.Records);   // impure
        var flows = records.Map(ScenarioListing.ListFlows);   // pure
        return flows.Map(each => ReportAll(each.Select(ScenarioListing.WriteFlowLine)));   // impure
    }

    /// <summary>
    /// <c>&lt;out&gt;/scenarios.json</c> from the runs' records, the selection and the flows drawn (each rule's PNG in its
    /// domain folder, or its error in <c>&lt;out&gt;/.flow-errors/&lt;rule&gt;.txt</c>), then the summary.
    /// </summary>
    public static Result<Unit, string> WriteScenarios(ToolCommand.WriteScenarios command)
    {
        var read = ReadRecords(command.Records);   // impure
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
        var flows = ScenarioListing.ListFlows(records.Value);   // pure
        var drawn = E2EFiles.FindDrawnFlows(command.Out, flows);   // impure
        var json = ScenarioListing.WriteScenarios(records.Value, selection, drawn);   // pure
        var written = E2EFiles.WriteFile(Path.Combine(command.Out, "scenarios.json"), json);   // impure
        return written.Map(_ => ReportAll(ScenarioListing.WriteSummary(records.Value, selection)));   // impure
    }

    /// <summary>The files a scenarios.json lists, a path a line.</summary>
    public static Result<Unit, string> ListFiles(ToolCommand.ListFiles command)
    {
        var json = E2EFiles.ReadFile(command.Scenarios);   // impure
        var files = json.Bind(JsonReading.ReadListedFiles).MapError(failure => $"{command.Scenarios}: {failure}");   // pure
        return files.Map(each => ReportAll(each));   // impure
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

    private static Result<ImmutableArray<RunRecord>, string> ReadRecords(ImmutableArray<string> paths) =>
        paths.Select(path => E2EFiles.ReadFile(path).Bind(JsonReading.ReadRunRecord).MapError(failure => $"{path}: {failure}"))
            .CombineAll()
            .MapError(failures => failures[0]);

    private static Unit Report(string line)
    {
        E2EFiles.Report(line);
        return new Unit.Value();
    }

    private static Unit ReportAll(IEnumerable<string> lines)
    {
        E2EFiles.ReportAll(lines);
        return new Unit.Value();
    }
}
