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
    partial record Play(PlayRequest Request);
    partial record Graph(GraphRequest Request);
    partial record Loop(LoopRequest Request);
    partial record Compare(CompareRequest Request);
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
          loopsmith play     <build.yaml> [--save <file.loop.yaml>] [--name "<loop name>"] [--why] [--caveats]
                                                              interactive: pick the next action, see what fires;
                                                              every action becomes a step of the loop you design
          loopsmith loop     <file.loop.yaml> [--cycles <n>] [--trace] [--why] [--caveats]
                                                              run a designed loop back to back: does it sustain?
          loopsmith compare  <a.loop.yaml> <b.loop.yaml> [--cycles <n>]   two designed loops side by side
          loopsmith loops    <build.yaml> [--limit <n>]       discovered loops (cycles that come back around)
          loopsmith graph    <build.yaml> [--loops-only] [--limit <n>]   Mermaid flowchart of the loop graph
          loopsmith validate <build.yaml>                     check the build against the rule catalog

        Options:
          --rules <dir>   rules directory (default: nearest rules/ above the build or loop file, then the current directory)
          --cycles <n>    cycles to run a loop for (default 10); a loop that completes all of them is sustainable
          --trace         also print every step of the loop's first cycle
          --save <file>   play: write the designed loop when you quit (w saves at any time)
          --name <name>   play: the designed loop's name (default: "<build name> loop")
          --no-color      plain output (also when NO_COLOR is set or output is redirected)

        Play commands:
          <number> or <action>  play it (and add it to the loop)    u  undo the last step
          n <note>              note on the last step               d <text>  loop description (\n = new line)
          a                     analyse the loop so far             w  save now
          e explain · s state · r reset · q quit (saves when --save is given)

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

        var command = rest[0];
        var fileCount = command == "compare" ? 2 : 1;
        var files = rest.Skip(1).Take(fileCount).ToImmutableArray();
        if (files.Length < fileCount || files.Any(file => file.StartsWith("--", StringComparison.Ordinal)))
        {
            return Fail(DescribeMissingFiles(command));
        }

        var options = ParseOptions(rest.Skip(1 + fileCount).ToImmutableArray());
        return options.Bind(o =>
        {
            var load = new LoadRequest(files[0], o.Rules);
            var trace = new TraceOptions(o.Flags.Contains("--state"), o.Flags.Contains("--why"), o.Flags.Contains("--caveats"));
            var loopTrace = o.Flags.Contains("--trace") ? Optional.Some(trace with { ShowState = true }) : Optional.None<TraceOptions>();
            return command switch
            {
                "explain" => Ok(new CliCommand.Explain(new ExplainRequest(load, o.Flags.Contains("--tree") ? ExplanationStyle.Tree : ExplanationStyle.Note)), noColor),
                "validate" => Ok(new CliCommand.Validate(load), noColor),
                "simulate" => Ok(new CliCommand.Simulate(new SimulateRequest(load, o.Actions, o.Scenario, trace)), noColor),
                "play" => Ok(new CliCommand.Play(new PlayRequest(load, trace, o.Save, o.Name)), noColor),
                "loop" => Ok(new CliCommand.Loop(new LoopRequest(files[0], o.Rules, o.Cycles, loopTrace)), noColor),
                "compare" => Ok(new CliCommand.Compare(new CompareRequest(files[0], files[1], o.Rules, o.Cycles)), noColor),
                "loops" => Ok(new CliCommand.Graph(new GraphRequest(load, GraphFormat.Loops, false, o.Limit)), noColor),
                "graph" => Ok(new CliCommand.Graph(new GraphRequest(load, GraphFormat.Mermaid, o.Flags.Contains("--loops-only"), o.Limit)), noColor),
                _ => Fail($"Unknown command '{command}'."),
            };
        });
    }

    private static string DescribeMissingFiles(string command) =>
        command switch
        {
            "loop" => "'loop' needs a loop file, e.g. loopsmith loop builds/skip-grenade-hunter/loops/infinite-skip-grenades.loop.yaml",
            "compare" => "'compare' needs two loop files, e.g. loopsmith compare builds/skip-grenade-hunter/loops/infinite-skip-grenades.loop.yaml builds/skip-grenade-hunter/loops/melee-first.loop.yaml",
            _ => $"'{command}' needs a build file, e.g. loopsmith {command} builds/skip-grenade-hunter/build.yaml",
        };

    private sealed record ParsedOptions(
        Optional<string> Rules,
        ImmutableArray<string> Actions,
        Optional<string> Scenario,
        int Limit,
        int Cycles,
        Optional<string> Save,
        Optional<string> Name,
        ImmutableHashSet<string> Flags);

    private static readonly ImmutableHashSet<string> KnownFlags =
        ["--tree", "--state", "--why", "--caveats", "--loops-only", "--trace"];

    private static Result<ParsedOptions, string> ParseOptions(ImmutableArray<string> args)
    {
        var parsed = new ParsedOptions(
            Optional.None<string>(), [], Optional.None<string>(), 10, LoopDesigning.DefaultMaxCycles, Optional.None<string>(), Optional.None<string>(), []);
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
                case "--cycles" when hasValue && int.TryParse(args[i + 1], out var cycles) && cycles > 0:
                    parsed = parsed with { Cycles = cycles };
                    i++;
                    break;
                case "--save" when hasValue:
                    parsed = parsed with { Save = Optional.Some(args[++i]) };
                    break;
                case "--name" when hasValue:
                    parsed = parsed with { Name = Optional.Some(args[++i]) };
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
