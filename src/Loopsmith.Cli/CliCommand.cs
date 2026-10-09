using System.Collections.Immutable;
using Dunet;
using Loopsmith.Core.BuildExplanation;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.TraceRendering;

namespace Loopsmith.Cli;

[Union]
public partial record CliCommand
{
    partial record Explain(ExplainRequest Request);
    partial record Validate(LoadRequest Request);
    partial record Simulate(SimulateRequest Request);
    partial record Play(LoadRequest Request, TraceOptions Options);
    partial record Graph(GraphRequest Request);
    partial record Help();
}

public sealed record CliInvocation(CliCommand Command, bool NoColor);

/// <summary>Pure: argv → command. Mapping in only — no behaviour.</summary>
public static class CliArguments
{
    public const string Usage = """
        loopsmith — see what your Destiny 2 build actually does

        Usage:
          loopsmith explain  <build.yaml> [--tree]            what each trigger sets off (note style; --tree aligned)
          loopsmith simulate <build.yaml> --actions <a,b,…>   step through a sequence of actions
                             [--scenario <file>] [--state] [--why] [--caveats]
          loopsmith play     <build.yaml> [--why] [--caveats] interactive: pick the next action, see what fires
          loopsmith loops    <build.yaml> [--limit <n>]       discovered loops (cycles that come back around)
          loopsmith graph    <build.yaml> [--loops-only] [--limit <n>]   Mermaid flowchart of the loop graph
          loopsmith validate <build.yaml>                     check the build against the rule catalog

        Options:
          --rules <dir>   rules directory (default: nearest rules/ above the build file, then the current directory)
          --no-color      plain output (also when NO_COLOR is set or output is redirected)

        Actions:
          grenade[:kill]  melee[:kill]  super[:kill]  class  kinetic|energy|power[:kill]  pickup:<id>  wait[:<seconds>]
          (without :kill the hit only damages)
        """;

    public static Result<CliInvocation, string> ParseArguments(ImmutableArray<string> args)
    {
        var noColor = args.Contains("--no-color");
        var rest = args.Where(a => a != "--no-color").ToImmutableArray();
        if (rest.IsEmpty || rest[0] is "help" or "--help" or "-h")
        {
            return Ok(new CliCommand.Help(), noColor);
        }

        if (rest.Length < 2 || rest[1].StartsWith("--", StringComparison.Ordinal))
        {
            return Fail($"'{rest[0]}' needs a build file, e.g. loopsmith {rest[0]} builds/skip-grenade-hunter/build.yaml");
        }

        var options = ParseOptions(rest.Skip(2).ToImmutableArray());
        return options.Bind(o =>
        {
            var load = new LoadRequest(rest[1], o.Rules);
            var trace = new TraceOptions(o.Flags.Contains("--state"), o.Flags.Contains("--why"), o.Flags.Contains("--caveats"));
            return rest[0] switch
            {
                "explain" => Ok(new CliCommand.Explain(new ExplainRequest(load, o.Flags.Contains("--tree") ? ExplanationStyle.Tree : ExplanationStyle.Note)), noColor),
                "validate" => Ok(new CliCommand.Validate(load), noColor),
                "simulate" => Ok(new CliCommand.Simulate(new SimulateRequest(load, o.Actions, o.Scenario, trace)), noColor),
                "play" => Ok(new CliCommand.Play(load, trace), noColor),
                "loops" => Ok(new CliCommand.Graph(new GraphRequest(load, GraphFormat.Loops, false, o.Limit)), noColor),
                "graph" => Ok(new CliCommand.Graph(new GraphRequest(load, GraphFormat.Mermaid, o.Flags.Contains("--loops-only"), o.Limit)), noColor),
                _ => Fail($"Unknown command '{rest[0]}'."),
            };
        });
    }

    private sealed record ParsedOptions(
        Optional<string> Rules, ImmutableArray<string> Actions, Optional<string> Scenario, int Limit, ImmutableHashSet<string> Flags);

    private static readonly ImmutableHashSet<string> KnownFlags =
        ["--tree", "--state", "--why", "--caveats", "--loops-only"];

    private static Result<ParsedOptions, string> ParseOptions(ImmutableArray<string> args)
    {
        var parsed = new ParsedOptions(Optional.None<string>(), [], Optional.None<string>(), 10, []);
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            var hasValue = i + 1 < args.Length;
            switch (arg)
            {
                case "--rules" when hasValue:
                    parsed = parsed with { Rules = Optional.Some(args[++i]) };
                    break;
                case "--actions" when hasValue:
                    parsed = parsed with { Actions = parsed.Actions.AddRange(args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) };
                    break;
                case "--scenario" when hasValue:
                    parsed = parsed with { Scenario = Optional.Some(args[++i]) };
                    break;
                case "--limit" when hasValue && int.TryParse(args[i + 1], out var limit) && limit > 0:
                    parsed = parsed with { Limit = limit };
                    i++;
                    break;
                case "--verbose":
                    parsed = parsed with { Flags = parsed.Flags.Add("--why").Add("--caveats") };
                    break;
                case var flag when KnownFlags.Contains(flag):
                    parsed = parsed with { Flags = parsed.Flags.Add(flag) };
                    break;
                default:
                    return new Result<ParsedOptions, string>.Error($"Unknown or incomplete option '{arg}'.");
            }
        }

        return new Result<ParsedOptions, string>.Ok(parsed);
    }

    private static Result<CliInvocation, string> Ok(CliCommand command, bool noColor) =>
        new Result<CliInvocation, string>.Ok(new CliInvocation(command, noColor));

    private static Result<CliInvocation, string> Fail(string message) =>
        new Result<CliInvocation, string>.Error(message);
}
