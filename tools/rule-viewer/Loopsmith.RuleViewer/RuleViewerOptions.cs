using Loopsmith.Core.Functional;

namespace Loopsmith.RuleViewer;

/// <summary>Where the tool reads the rules and writes the lanes: <c>--rules &lt;dir&gt;</c> and <c>--out &lt;dir&gt;</c>, by default the repository's <c>rules/</c> and <c>out/rule-viewer/</c>.</summary>
public sealed record RuleViewerOptions(string RulesDirectory, string OutDirectory)
{
    public const string Usage = "usage: dotnet run --project tools/rule-viewer/Loopsmith.RuleViewer [-- --rules <dir>] [--out <dir>]";

    public static Result<RuleViewerOptions, string> ReadOptions(IReadOnlyList<string> args, string repoRoot) =>
        ReadPairs(args, 0, new RuleViewerOptions(Path.Combine(repoRoot, "rules"), Path.Combine(repoRoot, "out", "rule-viewer")));

    private static Result<RuleViewerOptions, string> ReadPairs(IReadOnlyList<string> args, int at, RuleViewerOptions options) =>
        at >= args.Count
            ? new Result<RuleViewerOptions, string>.Ok(options)
            : args[at] is not ("--rules" or "--out")
                ? new Result<RuleViewerOptions, string>.Error($"Unknown option '{args[at]}'. {Usage}")
                : at + 1 >= args.Count
                    ? new Result<RuleViewerOptions, string>.Error($"'{args[at]}' needs a folder. {Usage}")
                    : ReadPairs(args, at + 2, args[at] == "--rules"
                        ? options with { RulesDirectory = args[at + 1] }
                        : options with { OutDirectory = args[at + 1] });
}
