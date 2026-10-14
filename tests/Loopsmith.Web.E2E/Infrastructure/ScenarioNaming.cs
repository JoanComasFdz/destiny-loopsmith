using Humanizer;

namespace Loopsmith.Web.E2E.Infrastructure;

/// <summary>A scenario's names, from its method name (<c>Given…_When…_Then…</c>) and its class's namespace.</summary>
public static class ScenarioNaming
{
    private const string ScenariosNamespace = "Loopsmith.Web.E2E.Scenarios";

    /// <summary>"Given app is open on home, When I click start from scratch, Then …": each part humanized.</summary>
    public static string ToTitle(string methodName) =>
        string.Join(", ", methodName.Split('_', StringSplitOptions.RemoveEmptyEntries).Select(part => part.Humanize()));

    /// <summary>"given-app-is-open-on-home-when-i-click-start-from-scratch-then-…": the scenario's GIF name.</summary>
    public static string ToGifName(string methodName) =>
        string.Join("-", methodName.Split('_', StringSplitOptions.RemoveEmptyEntries).Select(part => part.Kebaberize()));

    /// <summary>"Starting", "Build/Subclass": the class's domain folder under Scenarios/.</summary>
    public static string ToDomainFolder(Type testClass) =>
        (testClass.Namespace ?? "") is var name && name.StartsWith(ScenariosNamespace + ".", StringComparison.Ordinal)
            ? name[(ScenariosNamespace.Length + 1)..].Replace('.', '/')
            : "Unsorted";
}
