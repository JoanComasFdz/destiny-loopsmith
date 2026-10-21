using Loopsmith.E2ETools.Core;
using Loopsmith.E2ETools.Hosting;
using Loopsmith.Functional;

// Impure → pure → impure: read the options, then post the recorded scenarios on the PR, or clean up after it closed
// (tools/e2e/README.md). Every write goes through the gh CLI; --dry-run prints instead of writing.
var environmentRepo = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY");   // impure
var repo = Optional.FromNullable(environmentRepo);   // pure
var options = OptionsReading.ReadOptions(args, repo);   // pure
if (options is Result<E2EToolsOptions, string>.Error badOptions)
{
    E2EFiles.ReportError(badOptions.Failure);   // impure
    return 2;
}

var given = ((Result<E2EToolsOptions, string>.Ok)options).Value;
var done = given.Command is E2ECommand.PostScenarios post
    ? ScenarioPosting.PostScenarios(given, post)
    : PrClosing.ClosePr(given, (E2ECommand.ClosePr)given.Command);   // impure
if (done is Result<Unit, string>.Error failed)
{
    E2EFiles.ReportError(failed.Failure);   // impure
    return 1;
}

return 0;
