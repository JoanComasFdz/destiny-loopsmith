using System.Collections.Immutable;
using System.Text;
using Loopsmith.Cli;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;

// Host only: map argv in, call an Orchestration shell, execute its effects (console and file I/O).
Console.OutputEncoding = Encoding.UTF8;                                                     // impure

var invocation = CliArguments.ParseArguments([.. args]);                                    // pure
var noColorFlag = invocation.Match(ok => ok.Value.NoColor, _ => true);                      // pure
var noColorEnvironment = Environment.GetEnvironmentVariable("NO_COLOR");                    // impure
var isRedirected = Console.IsOutputRedirected;                                              // impure
var useColor = !noColorFlag && noColorEnvironment is null && !isRedirected;                 // pure

var exitCode = invocation.Match(
    ok => RunCommand(ok.Value.Command, useColor),                                           // impure
    error => WriteUsageError(error.Failure));                                               // impure
return exitCode;

static int RunCommand(CliCommand command, bool useColor) =>
    command.Match(
        explain => RunShell(() => CommandShells.RunExplain(explain.Request), useColor),
        validate => RunShell(() => CommandShells.RunValidate(validate.Request), useColor),
        trace => RunShell(() => CommandShells.RunTrace(trace.Request), useColor),
        play => RunPlay(play, useColor),
        graph => RunShell(() => CommandShells.RunGraph(graph.Request), useColor),
        loop => RunShell(() => CommandShells.RunLoop(loop.Request), useColor),
        compare => RunShell(() => CommandShells.RunCompare(compare.Request), useColor),
        _ => WriteUsage());

static int RunShell(Func<ImmutableArray<Effect>> shell, bool useColor)
{
    var effects = shell();                                                                   // impure
    var code = ExecuteEffects(effects, useColor);                                            // impure
    return code;
}

static int RunPlay(CliCommand.Play play, bool useColor)
{
    var started = PlaySessions.StartPlay(play.Request);                                     // impure

    var exitCode = started.Match(
        ok => RunPlayLoop(ok.Value, useColor),                                               // impure
        error => ExecuteEffects([new Effect.ShowFailure(error.Failure)], useColor));         // impure
    return exitCode;
}

static int RunPlayLoop(PlaySession session, bool useColor)
{
    var opening = PlaySessions.PlanOpening(session);                                         // pure
    var exitCode = ExecuteEffects(opening, useColor);                                        // impure
    while (!session.IsOver)
    {
        Console.Write("> ");                                                                 // impure
        var input = Console.ReadLine();                                                      // impure
        var line = input ?? "quit";                                                          // pure: end of input quits
        var (next, effects) = PlaySessions.StepPlay(session, line);                          // impure
        exitCode = ExecuteEffects(effects, useColor);                                        // impure: the last turn (quit + save) decides
        session = next;
    }

    return exitCode;
}

static int ExecuteEffects(ImmutableArray<Effect> effects, bool useColor) =>
    effects.Aggregate(0, (exit, effect) =>
    {
        var code = ExecuteEffect(effect, useColor);                                          // impure
        return Math.Max(exit, code);
    });

static int ExecuteEffect(Effect effect, bool useColor) =>
    effect.Match(
        lines =>
        {
            foreach (var line in lines.Lines)
            {
                var text = AnsiRendering.ToTerminalText(line, useColor);                     // pure
                Console.WriteLine(text);                                                     // impure
            }

            return 0;
        },
        text =>
        {
            Console.Write(text.Text);                                                        // impure
            return 0;
        },
        failure =>
        {
            Console.Error.WriteLine(failure.Message);                                        // impure
            return 1;
        },
        save => SaveFile(save, useColor));                                                   // impure

static int SaveFile(Effect.SaveFile save, bool useColor)
{
    var written = WriteTextFile(save.Path, save.Text);                                       // impure
    var outcome = written.Match(                                                             // pure
        _ => (Effect)new Effect.WriteLines(save.Saved),
        error => new Effect.ShowFailure(error.Failure));
    var code = ExecuteEffect(outcome, useColor);                                             // impure
    return code;
}

/// <summary>Writes (or replaces) a UTF-8 text file, creating its folder if needed.</summary>
static Result<Unit, string> WriteTextFile(string path, string text)
{
    try
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);                                            // impure
        }

        File.WriteAllText(path, text);                                                       // impure
        return new Result<Unit, string>.Ok(new Unit.Value());
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
    {
        return new Result<Unit, string>.Error($"Cannot write '{path}': {exception.Message}");
    }
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
