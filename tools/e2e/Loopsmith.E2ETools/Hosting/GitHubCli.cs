using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Loopsmith.Functional;

namespace Loopsmith.E2ETools.Hosting;

/// <summary>What a command answered: its exit code, what it wrote and what it said on error.</summary>
public sealed record CommandAnswer(int ExitCode, string Output, string Errors);

/// <summary>
/// Impure boundary: the gh CLI (GH_TOKEN) and git, run as child processes. <see cref="RunGh"/> gives the answer as it is,
/// for a call whose refusal is a warning; <see cref="CallGh"/> gives the output, or an error saying what gh said.
/// </summary>
public static class GitHubCli
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    public static CommandAnswer RunGh(IReadOnlyList<string> arguments, Optional<string> input) => RunCommand("gh", arguments, input);

    public static Result<string, string> CallGh(IReadOnlyList<string> arguments, Optional<string> input)
    {
        var answer = RunGh(arguments, input);
        return answer.ExitCode == 0
            ? new Result<string, string>.Ok(answer.Output)
            : new Result<string, string>.Error($"gh {string.Join(' ', arguments)} failed:\n{answer.Errors.Trim()}");
    }

    /// <summary>The full message of a commit (<c>git log -1 --format=%B</c>).</summary>
    public static Result<string, string> ReadCommitMessage(string commit)
    {
        var answer = RunCommand("git", ["log", "-1", "--format=%B", commit], Optional.None<string>());
        return answer.ExitCode == 0
            ? new Result<string, string>.Ok(answer.Output)
            : new Result<string, string>.Error($"git log {commit} failed:\n{answer.Errors.Trim()}");
    }

    private static CommandAnswer RunCommand(string program, IReadOnlyList<string> arguments, Optional<string> input)
    {
        var start = new ProcessStartInfo(program)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardInputEncoding = Utf8,
            StandardOutputEncoding = Utf8,
            StandardErrorEncoding = Utf8,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(start)!;
            var errors = process.StandardError.ReadToEndAsync();
            process.StandardInput.Write(input.UnwrapOr(""));
            process.StandardInput.Close();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return new CommandAnswer(process.ExitCode, output, errors.Result);
        }
        catch (Win32Exception exception)
        {
            return new CommandAnswer(127, "", $"{program} couldn't start: {exception.Message}");
        }
    }
}
