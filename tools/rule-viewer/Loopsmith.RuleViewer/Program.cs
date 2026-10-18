using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.RuleViewer.Core;
using Loopsmith.RuleViewer;
using Loopsmith.RuleViewer.Hosting;
using Loopsmith.RuleViewer.Page;

// Impure → pure → impure: read the rules and the page's style and script, draw the flows, write their files.
var repoRoot = RuleViewerFiles.FindRepoRoot(Directory.GetCurrentDirectory());   // impure
var options = RuleViewerOptions.ReadOptions(args, repoRoot);   // pure
if (options is Result<RuleViewerOptions, string>.Error badOptions)
{
    RuleViewerFiles.ReportError(badOptions.Failure);   // impure
    return 2;
}

var paths = ((Result<RuleViewerOptions, string>.Ok)options).Value;
var ruleFiles = RuleViewerFiles.ReadRuleFiles(paths.RulesDirectory);   // impure
var assets = RuleViewerFiles.ReadPageAssets(typeof(RuleViewerFiles).Assembly);   // impure
var rules = ruleFiles.Bind(files => RuleViewing.DrawRules(files, GuardianClass.Hunter, Subclass.Arc));   // pure
var pages = rules.Bind(drawn => assets.Map(page => (Json: RuleViewing.RenderRulesJson(drawn), Html: RulePage.RenderPage(drawn, page))));   // pure
if (pages is Result<(string Json, string Html), string>.Error failed)
{
    RuleViewerFiles.ReportError(failed.Failure);   // impure
    return 1;
}

var written = ((Result<(string Json, string Html), string>.Ok)pages).Value;
var jsonPath = Path.Combine(paths.OutDirectory, "arc-hunter-rules.json");   // pure
var htmlPath = Path.Combine(paths.OutDirectory, "arc-hunter-rules.html");   // pure
var saved = RuleViewerFiles.WriteFile(jsonPath, written.Json).Bind(_ => RuleViewerFiles.WriteFile(htmlPath, written.Html));   // impure
return saved.Match(
    _ =>
    {
        RuleViewerFiles.Report($"Wrote {jsonPath} and {htmlPath}");   // impure
        return 0;
    },
    error =>
    {
        RuleViewerFiles.ReportError(error.Failure);   // impure
        return 1;
    });
