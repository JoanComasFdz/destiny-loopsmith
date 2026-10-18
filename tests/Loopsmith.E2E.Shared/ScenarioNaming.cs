using System.Text;
using Humanizer;

namespace Loopsmith.E2E.Shared;

/// <summary>A scenario's names, from its Gherkin's title, its method's name and its class's namespace.</summary>
public static class ScenarioNaming
{
    private const string ScenariosNamespace = ".Scenarios.";
    private const string RulesNamespace = ".Rules.";

    /// <summary>
    /// "Reaper's Tithe gives 3 stacks for a kill of 3" → "ReapersTitheGives3StacksForAKillOf3": the method's name for a
    /// Gherkin's title. Each run of letters and digits is a word (an apostrophe joins its word), its first letter upper case.
    /// </summary>
    public static string ToMethodName(string title)
    {
        var name = new StringBuilder();
        var startsWord = true;
        foreach (var character in title.Replace("'", "").Replace("’", ""))
        {
            if (!char.IsAsciiLetterOrDigit(character))
            {
                startsWord = true;
                continue;
            }

            name.Append(startsWord ? char.ToUpperInvariant(character) : character);
            startsWord = false;
        }

        return name.ToString();
    }

    /// <summary>"start-from-scratch-opens-an-arc-hunter-with-nothing-equipped-mobile": a run's GIF name.</summary>
    public static string ToGifName(string methodName, Screen screen) =>
        $"{methodName.Kebaberize()}-{screen.ToString().ToLowerInvariant()}";

    /// <summary>
    /// "Starting", "Build/Subclass": a scenario class's domain folder under its project's Scenarios/, its namespace after
    /// ".Scenarios."; "Rules/Hunter/Arc/Melees": a rule scenario's, under Rules/, its namespace from "Rules" on (the
    /// VS Code extension reads it from a scenario file the same way, to find its recordings; the "E2E videos" workflow
    /// names a rule scenario's release after it: docs/hosting.md, "E2E videos on pull requests").
    /// </summary>
    public static string ToDomainFolder(Type testClass) => ToDomainFolder(testClass.Namespace);

    /// <inheritdoc cref="ToDomainFolder(Type)"/>
    public static string ToDomainFolder(string? @namespace) =>
        (@namespace ?? "") is var name && name.IndexOf(ScenariosNamespace, StringComparison.Ordinal) is var at && at >= 0
            ? name[(at + ScenariosNamespace.Length)..].Replace('.', '/')
            : name.IndexOf(RulesNamespace, StringComparison.Ordinal) is var rules && rules >= 0
                ? name[(rules + 1)..].Replace('.', '/')
                : "Unsorted";
}
