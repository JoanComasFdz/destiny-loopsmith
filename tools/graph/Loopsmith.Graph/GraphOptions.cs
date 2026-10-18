using Loopsmith.Core.Functional;

namespace Loopsmith.Graph;

/// <summary>Where the tool reads the rules and writes the lanes: <c>--rules &lt;dir&gt;</c> and <c>--out &lt;dir&gt;</c>, by default the repository's <c>rules/</c> and <c>out/graph/</c>.</summary>
public sealed record GraphOptions(string RulesDirectory, string OutDirectory)
{
    public const string Usage = "usage: dotnet run --project tools/graph/Loopsmith.Graph [-- --rules <dir>] [--out <dir>]";

    public static Result<GraphOptions, string> ReadOptions(IReadOnlyList<string> args, string repoRoot) =>
        ReadPairs(args, 0, new GraphOptions(Path.Combine(repoRoot, "rules"), Path.Combine(repoRoot, "out", "graph")));

    private static Result<GraphOptions, string> ReadPairs(IReadOnlyList<string> args, int at, GraphOptions options) =>
        at >= args.Count
            ? new Result<GraphOptions, string>.Ok(options)
            : args[at] is not ("--rules" or "--out")
                ? new Result<GraphOptions, string>.Error($"Unknown option '{args[at]}'. {Usage}")
                : at + 1 >= args.Count
                    ? new Result<GraphOptions, string>.Error($"'{args[at]}' needs a folder. {Usage}")
                    : ReadPairs(args, at + 2, args[at] == "--rules"
                        ? options with { RulesDirectory = args[at + 1] }
                        : options with { OutDirectory = args[at + 1] });
}
