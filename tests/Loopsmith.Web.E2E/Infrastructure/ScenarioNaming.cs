using System.Text;
using Humanizer;

namespace Loopsmith.Web.E2E.Infrastructure;

/// <summary>A scenario's names, from its Gherkin's title, its method's name and its class's namespace.</summary>
public static class ScenarioNaming
{
    private const string ScenariosNamespace = "Loopsmith.Web.E2E.Scenarios";

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

    /// <summary>"Starting", "Build/Subclass": the class's domain folder under Scenarios/.</summary>
    public static string ToDomainFolder(Type testClass) =>
        (testClass.Namespace ?? "") is var name && name.StartsWith(ScenariosNamespace + ".", StringComparison.Ordinal)
            ? name[(ScenariosNamespace.Length + 1)..].Replace('.', '/')
            : "Unsorted";
}
