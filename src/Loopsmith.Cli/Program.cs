using System.Collections.Immutable;
using System.Text;
using Loopsmith.Cli;
using Loopsmith.Core.Orchestration;

// Host only: map argv in, call an Orchestration shell, write its effects out.
Console.OutputEncoding = Encoding.UTF8;

var invocation = CliArguments.ParseArguments([.. args]);                                    // pure
var useColor = invocation.Match(ok => !ok.Value.NoColor, _ => false)
    && Environment.GetEnvironmentVariable("NO_COLOR") is null
    && !Console.IsOutputRedirected;                                                         // impure

var exitCode = invocation.Match(
    ok => RunCommand(ok.Value.Command, useColor),                                           // impure
    error => WriteUsageError(error.Failure));                                               // impure
return exitCode;

static int RunCommand(CliCommand command, bool useColor) =>
    command.Match(
        explain => ExecuteEffects(CommandShells.RunExplain(explain.Request), useColor),
        validate => ExecuteEffects(CommandShells.RunValidate(validate.Request), useColor),
        simulate => ExecuteEffects(CommandShells.RunSimulate(simulate.Request), useColor),
        play => RunPlay(play, useColor),
        graph => ExecuteEffects(CommandShells.RunGraph(graph.Request), useColor),
        _ => WriteUsage());

static int RunPlay(CliCommand.Play play, bool useColor)
{
    var started = PlaySessions.StartPlay(play.Request, play.Options);                       // impure

    var exitCode = started.Match(
        ok => RunPlayLoop(ok.Value, useColor),                                               // impure
        error => ExecuteEffects([new Effect.ShowFailure(error.Failure)], useColor));         // impure
    return exitCode;
}

static int RunPlayLoop(PlaySession session, bool useColor)
{
    var opening = PlaySessions.PlanOpening(session);                                         // pure
    ExecuteEffects(opening, useColor);                                                       // impure
    while (!session.IsOver)
    {
        Console.Write("> ");                                                                 // impure
        var input = Console.ReadLine();                                                      // impure
        if (input is null)
        {
            break;
        }

        var (next, effects) = PlaySessions.AdvancePlay(session, input);                      // pure
        ExecuteEffects(effects, useColor);                                                   // impure
        session = next;
    }

    return 0;
}

static int ExecuteEffects(ImmutableArray<Effect> effects, bool useColor)
{
    var failed = false;
    foreach (var effect in effects)
    {
        effect.Match(
            lines =>
            {
                foreach (var line in lines.Lines)
                {
                    var text = AnsiRendering.ToTerminalText(line, useColor);                 // pure
                    Console.WriteLine(text);                                                 // impure
                }

                return 0;
            },
            text =>
            {
                Console.Write(text.Text);                                                    // impure
                return 0;
            },
            failure =>
            {
                failed = true;
                Console.Error.WriteLine(failure.Message);                                    // impure
                return 1;
            });
    }

    return failed ? 1 : 0;
}

static int WriteUsage()
{
    Console.WriteLine(CliArguments.Usage);                                                   // impure
    return 0;
}

static int WriteUsageError(string message)
{
    Console.Error.WriteLine(message);                                                        // impure
    Console.Error.WriteLine();                                                               // impure
    Console.Error.WriteLine(CliArguments.Usage);                                             // impure
    return 2;
}
