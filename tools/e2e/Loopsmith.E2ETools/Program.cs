using Loopsmith.E2ETools;
using Loopsmith.E2ETools.Hosting;
using Loopsmith.Functional;

// Impure → pure → impure: read the options, then post the recorded scenarios on the PR, or clean up after it closed
// (tools/e2e/README.md). Every write goes through the gh CLI; --dry-run prints instead of writing.
var environmentRepo = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY");   // impure
var options = E2EToolsOptions.ReadOptions(args, Optional.FromNullable(environmentRepo).Bind(repo => repo.Length > 0 ? Optional.Some(repo) : Optional.None<string>()));   // pure
if (options is Result<E2EToolsOptions, string>.Error badOptions)
{
    E2EFiles.ReportError(badOptions.Failure);   // impure
    return 2;
}

var given = ((Result<E2EToolsOptions, string>.Ok)options).Value;
var done = given.Command.Match(
    post => ScenarioPosting.PostScenarios(given, post),
    close => PrClosing.ClosePr(given, close));   // impure
return done.Match(
    _ => 0,
    error =>
    {
        E2EFiles.ReportError(error.Failure);   // impure
        return 1;
    });
