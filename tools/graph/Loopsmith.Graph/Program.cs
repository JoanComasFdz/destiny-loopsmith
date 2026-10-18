using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Graph;
using Loopsmith.Graph.Hosting;
using Loopsmith.Graph.Page;

// Impure → pure → impure: read the rules and the page's style and script, draw the lanes, write their files.
var repoRoot = GraphFiles.FindRepoRoot(Directory.GetCurrentDirectory());   // impure
var options = GraphOptions.ReadOptions(args, repoRoot);   // pure
if (options is Result<GraphOptions, string>.Error badOptions)
{
    GraphFiles.ReportError(badOptions.Failure);   // impure
    return 2;
}

var paths = ((Result<GraphOptions, string>.Ok)options).Value;
var ruleFiles = GraphFiles.ReadRuleFiles(paths.RulesDirectory);   // impure
var assets = GraphFiles.ReadPageAssets(typeof(GraphFiles).Assembly);   // impure
var lanes = ruleFiles.Bind(files => DependencyGraphs.DrawLanes(files, GuardianClass.Hunter, Subclass.Arc));   // pure
var pages = lanes.Bind(drawn => assets.Map(page => (Json: DependencyGraphs.RenderLanesJson(drawn), Html: LanePage.RenderPage(drawn, page))));   // pure
if (pages is Result<(string Json, string Html), string>.Error failed)
{
    GraphFiles.ReportError(failed.Failure);   // impure
    return 1;
}

var written = ((Result<(string Json, string Html), string>.Ok)pages).Value;
var jsonPath = Path.Combine(paths.OutDirectory, "arc-hunter-lanes.json");   // pure
var htmlPath = Path.Combine(paths.OutDirectory, "arc-hunter-lanes.html");   // pure
var saved = GraphFiles.WriteFile(jsonPath, written.Json).Bind(_ => GraphFiles.WriteFile(htmlPath, written.Html));   // impure
return saved.Match(
    _ =>
    {
        GraphFiles.Report($"Wrote {jsonPath} and {htmlPath}");   // impure
        return 0;
    },
    error =>
    {
        GraphFiles.ReportError(error.Failure);   // impure
        return 1;
    });
