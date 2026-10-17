using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Graph;
using Loopsmith.Graph.Hosting;

// Impure → pure → impure: read the rules and the template, draw the graph, write its files.
var repoRoot = GraphFiles.FindRepoRoot(Directory.GetCurrentDirectory());   // impure
var options = GraphOptions.ReadOptions(args, repoRoot);   // pure
if (options is Result<GraphOptions, string>.Error badOptions)
{
    GraphFiles.ReportError(badOptions.Failure);   // impure
    return 2;
}

var paths = ((Result<GraphOptions, string>.Ok)options).Value;
var ruleFiles = GraphFiles.ReadRuleFiles(paths.RulesDirectory);   // impure
var template = GraphFiles.ReadPageTemplate(typeof(GraphFiles).Assembly);   // impure
var pages = ruleFiles.Bind(files => template.Bind(page => DependencyGraphs.DrawGraphPages(files, page, GuardianClass.Hunter, Subclass.Arc)));   // pure
if (pages is Result<GraphPages, string>.Error failed)
{
    GraphFiles.ReportError(failed.Failure);   // impure
    return 1;
}

var drawn = ((Result<GraphPages, string>.Ok)pages).Value;
var jsonPath = Path.Combine(paths.OutDirectory, "arc-hunter.json");   // pure
var htmlPath = Path.Combine(paths.OutDirectory, "arc-hunter.html");   // pure
var written = GraphFiles.WriteFile(jsonPath, drawn.Json).Bind(_ => GraphFiles.WriteFile(htmlPath, drawn.Html));   // impure
return written.Match(
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
