using Loopsmith.E2ETools.Core;
using Loopsmith.E2ETools.Hosting;
using Loopsmith.Functional;

// Impure → pure → impure: read the command, then post the recorded scenarios on the PR, clean up after it closed, or run
// one of the steps the e2e scripts call (tools/e2e/README.md). Every write to GitHub goes through the gh CLI; --dry-run
// prints instead of writing.
var environmentRepo = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY");   // impure
var repo = Optional.FromNullable(environmentRepo);   // pure
var command = CommandReading.ReadCommand(args, repo);   // pure
if (command is Result<ToolCommand, string>.Error badCommand)
{
    E2EFiles.ReportError(badCommand.Failure);   // impure
    return 2;
}

var done = ((Result<ToolCommand, string>.Ok)command).Value switch
{
    ToolCommand.Post { Options.Command: E2ECommand.PostScenarios post } given => ScenarioPosting.PostScenarios(given.Options, post),
    ToolCommand.Post given => PrClosing.ClosePr(given.Options, (E2ECommand.ClosePr)given.Options.Command),
    ToolCommand.FindFreePort => RecordingSteps.FindFreePort(),
    ToolCommand.ReadVsCodeCommit read => RecordingSteps.ReadVsCodeCommit(read),
    ToolCommand.ReportRuns report => RecordingSteps.ReportRuns(report),
    ToolCommand.FindHome find => RecordingSteps.FindHome(find),
    ToolCommand.ListFlows list => RecordingSteps.ListFlows(list),
    ToolCommand.WriteScenarios write => RecordingSteps.WriteScenarios(write),
    ToolCommand.ListFiles list => RecordingSteps.ListFiles(list),
    ToolCommand.PackVsix pack => RecordingSteps.PackVsix(pack),
    _ => new Result<Unit, string>.Error("Unknown command."),
};   // impure
if (done is Result<Unit, string>.Error failed)
{
    E2EFiles.ReportError(failed.Failure);   // impure
    return 1;
}

return 0;
