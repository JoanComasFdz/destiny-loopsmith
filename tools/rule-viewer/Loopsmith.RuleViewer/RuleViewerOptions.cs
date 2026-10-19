using Loopsmith.Core.Functional;
using Loopsmith.RuleViewer.Page;

namespace Loopsmith.RuleViewer;

/// <summary>
/// Where the tool reads the rules and writes the flows: <c>--rules &lt;dir&gt;</c> and <c>--out &lt;dir&gt;</c>, by default the
/// repository's <c>rules/</c> and <c>out/rule-viewer/</c>; and, with <c>--rule &lt;name&gt;</c>, the one rule whose flows it
/// draws as a PNG instead of the page (its full name, <c>hunter-arc-aspects-lethal-current-2</c>), in the theme
/// <c>--theme light|dark</c> names (light by default).
/// </summary>
public sealed record RuleViewerOptions(string RulesDirectory, string OutDirectory, Optional<string> Rule, FlowTheme Theme)
{
    public const string Usage = "usage: dotnet run --project tools/rule-viewer/Loopsmith.RuleViewer [-- --rules <dir>] [--out <dir>] [--rule <rule name>] [--theme light|dark]";

    public static Result<RuleViewerOptions, string> ReadOptions(IReadOnlyList<string> args, string repoRoot) =>
        ReadPairs(args, 0, new RuleViewerOptions(Path.Combine(repoRoot, "rules"), Path.Combine(repoRoot, "out", "rule-viewer"), Optional.None<string>(), FlowTheme.Light));

    private static Result<RuleViewerOptions, string> ReadPairs(IReadOnlyList<string> args, int at, RuleViewerOptions options) =>
        at >= args.Count
            ? new Result<RuleViewerOptions, string>.Ok(options)
            : args[at] is not ("--rules" or "--out" or "--rule" or "--theme")
                ? new Result<RuleViewerOptions, string>.Error($"Unknown option '{args[at]}'. {Usage}")
                : at + 1 >= args.Count
                    ? new Result<RuleViewerOptions, string>.Error($"'{args[at]}' needs {DescribeValue(args[at])}. {Usage}")
                    : args[at] == "--theme" && args[at + 1] is not ("light" or "dark")
                        ? new Result<RuleViewerOptions, string>.Error($"'--theme' is light or dark, not '{args[at + 1]}'. {Usage}")
                    : ReadPairs(args, at + 2, args[at] switch
                    {
                        "--rules" => options with { RulesDirectory = args[at + 1] },
                        "--out" => options with { OutDirectory = args[at + 1] },
                        "--theme" => options with { Theme = args[at + 1] == "dark" ? FlowTheme.Dark : FlowTheme.Light },
                        _ => options with { Rule = Optional.Some(args[at + 1]) },
                    });

    private static string DescribeValue(string option) =>
        option switch
        {
            "--rule" => "a rule name",
            "--theme" => "light or dark",
            _ => "a folder",
        };
}
