using Dunet;

namespace Loopsmith.Web.E2E.Infrastructure.Vocabulary;

/// <summary>
/// A subclass's ability (its super, grenade, melee or class ability), as the trigger panel's square and a step card name
/// it. A case is added when a test needs it.
/// </summary>
[Union]
public partial record Ability
{
    public partial record ArcStaff;
    public partial record GatheringStorm;
    public partial record StormsEdge;
    public partial record SkipGrenade;
    public partial record CombinationBlow;
    public partial record DisorientingBlow;
    public partial record GamblersDodge;
    public partial record MarksmansDodge;

    /// <summary>What the UI shows that no case names yet: a failing assertion names it as the UI shows it.</summary>
    public partial record Unlisted(string Shown);
}

/// <summary>What the UI shows for each <see cref="Ability"/>: the only place those strings live.</summary>
public static class AbilityShowing
{
    /// <summary>What the UI shows for <paramref name="value"/>.</summary>
    public static string ToShownText(this Ability value) =>
        value.Match(
            arcStaff => "Arc Staff",
            gatheringStorm => "Gathering Storm",
            stormsEdge => "Storm's Edge",
            skipGrenade => "Skip Grenade",
            combinationBlow => "Combination Blow",
            disorientingBlow => "Disorienting Blow",
            gamblersDodge => "Gambler's Dodge",
            marksmansDodge => "Marksman's Dodge",
            unlisted => unlisted.Shown);
}
