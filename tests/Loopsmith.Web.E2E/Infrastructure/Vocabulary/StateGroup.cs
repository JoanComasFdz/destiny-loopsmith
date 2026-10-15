using Dunet;

namespace Loopsmith.Web.E2E.Infrastructure.Vocabulary;

/// <summary>A group of a step card's "State after #N": what is present after the step.</summary>
[Union]
public partial record StateGroup
{
    public partial record Buffs;
    public partial record Weapons;
    public partial record Debuffs;
    public partial record Ground;
}

/// <summary>What the UI shows for each <see cref="StateGroup"/>: the only place those strings live.</summary>
public static class StateGroupShowing
{
    /// <summary>What the UI shows for <paramref name="value"/> (in capitals, as the card draws it).</summary>
    public static string ToShownText(this StateGroup value) =>
        value.Match(
            buffs => "BUFFS",
            weapons => "WEAPONS",
            debuffs => "DEBUFFS",
            ground => "GROUND");
}
