using System.Reflection;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

namespace Loopsmith.E2E.Shared;

/// <summary>
/// The screen a scenario is played on (E2E-TEST-CODE-RULES.md, rule a): a scenario's <c>[PlayInDesktop]</c> and
/// <c>[PlayInMobile]</c> each play it once, and the project's <c>BaseE2E</c> opens the browser as that screen.
/// </summary>
public enum Screen
{
    /// <summary>A desktop window, 1280×800, with a mouse.</summary>
    Desktop,

    /// <summary>A phone, 390×844, with touch: the app's touch styles apply (<c>(hover: none)</c>, <c>(pointer: coarse)</c>).</summary>
    Mobile,
}

/// <summary>What the UI shows for each <see cref="Screen"/>, and its Gherkin tag.</summary>
public static class ScreenShowing
{
    /// <summary>"🖥️ Desktop", "📱 Mobile": the run's label in the test's name and the PR's subtitle.</summary>
    public static string ToLabel(this Screen screen) =>
        screen switch
        {
            Screen.Desktop => "🖥️ Desktop",
            _ => "📱 Mobile",
        };

    /// <summary>"@desktop", "@mobile": the Gherkin tag that asks for the screen.</summary>
    public static string ToTag(this Screen screen) => "@" + screen.ToString().ToLowerInvariant();
}

/// <summary>Plays the scenario on a desktop window (<see cref="Screen.Desktop"/>). At most once per scenario.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class PlayInDesktopAttribute() : PlayInAttribute(Screen.Desktop);

/// <summary>Plays the scenario on a phone (<see cref="Screen.Mobile"/>). At most once per scenario.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class PlayInMobileAttribute() : PlayInAttribute(Screen.Mobile);

/// <summary>
/// One play of a scenario on <see cref="Screen"/>: a theory row whose one argument is the screen (xunit merges two
/// rows with the same arguments, so the screen is the argument, not only the label), labelled "🖥️ Desktop" or
/// "📱 Mobile".
/// </summary>
public abstract class PlayInAttribute(Screen screen) : DataAttribute
{
    /// <summary>The screen this attribute plays the scenario on.</summary>
    public Screen Screen { get; } = screen;

    public override ValueTask<IReadOnlyCollection<ITheoryDataRow>> GetData(MethodInfo testMethod, DisposalTracker disposalTracker) =>
        new([new TheoryDataRow<Screen>(Screen) { Label = Screen.ToLabel() }]);

    public override bool SupportsDiscoveryEnumeration() => true;
}
