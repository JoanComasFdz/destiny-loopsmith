using Loopsmith.Functional;
using Loopsmith.RuleViewer.Core;
using Loopsmith.RuleViewer;
using Loopsmith.RuleViewer.Hosting;
using Loopsmith.RuleViewer.Page;

// Impure → pure → impure: read the rules and the page's style and script, draw the flows, write their files (or, with
// --rule, one rule's flows as a PNG, whichever rule it is).
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
if (paths.Rule is Optional<string>.Some rule)
{
    // One rule's flows, wherever its file sits under the rules → its page → its PNG, named by the rule.
    var flowPage = ruleFiles.Bind(files => RuleViewing.PickRuleFlows(files, rule.Value)).Bind(flows => assets.Map(page => RuleFlowPage.RenderPage(rule.Value, flows, page, paths.Theme)));   // pure
    if (flowPage is Result<string, string>.Error noFlows)
    {
        RuleViewerFiles.ReportError(noFlows.Failure);   // impure
        return 1;
    }

    var pngPath = Path.Combine(paths.OutDirectory, $"{rule.Value}.png");   // pure
    var html = ((Result<string, string>.Ok)flowPage).Value;
    var shot = await RuleFlowShot.SaveShotAsync(html, RuleFlowPage.ShotSelector, pngPath, paths.Theme);   // impure
    return shot.Match(
        _ =>
        {
            RuleViewerFiles.Report($"Wrote {pngPath}");   // impure
            return 0;
        },
        error =>
        {
            RuleViewerFiles.ReportError(error.Failure);   // impure
            return 1;
        });
}

var tree = ruleFiles.Bind(RuleViewing.DrawTree);   // pure
var pages = tree.Bind(drawn => assets.Map(page => (Json: RuleViewing.RenderTreeJson(drawn), Html: RulePage.RenderPage(drawn, page))));   // pure
if (pages is Result<(string Json, string Html), string>.Error failed)
{
    RuleViewerFiles.ReportError(failed.Failure);   // impure
    return 1;
}

var written = ((Result<(string Json, string Html), string>.Ok)pages).Value;
var jsonPath = Path.Combine(paths.OutDirectory, "rules.json");   // pure
var htmlPath = Path.Combine(paths.OutDirectory, "rules.html");   // pure
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
