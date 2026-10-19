using System.Collections.Immutable;
using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>What a test checks on a step's card.</summary>
public static class StepCardSnapshotAssertions
{
    private const string Chance = "(chance)";

    /// <summary>The card's heading is <paramref name="expected"/> ("#1 Festival Flight (🥊 hit)").</summary>
    public static AssertingThat<StepCardSnapshot> HasHeading(this AssertingThat<StepCardSnapshot> that, string expected)
    {
        var shown = that.InstanceToAssert.Heading;
        Assert.True(shown == expected, $"The card's heading is \"{shown}\", but \"{expected}\" was expected.");
        return that;
    }

    /// <summary>The card's first event reads <paramref name="expected"/> ("Festival Flight 🥊 hit 1 enemy").</summary>
    public static AssertingThat<StepCardSnapshot> Reads(this AssertingThat<StepCardSnapshot> that, string expected)
    {
        var events = that.InstanceToAssert.Events;
        var shown = events.IsEmpty ? "nothing" : $"\"{events[0].Text}\"";
        Assert.True(!events.IsEmpty && events[0].Text == expected, $"The card reads {shown}, but \"{expected}\" was expected.");
        return that;
    }

    /// <summary>The card reads "nothing triggers": no event and no line.</summary>
    public static AssertingThat<StepCardSnapshot> ReadsNothingTriggers(this AssertingThat<StepCardSnapshot> that)
    {
        var shown = that.InstanceToAssert;
        Assert.True(
            shown.NothingTriggers && shown.Events.IsEmpty,
            $"The card reads {Describe(shown.Events)}, but \"nothing triggers\" was expected.");
        return that;
    }

    /// <summary><paramref name="perk"/> fired exactly <paramref name="lines"/>, in that order, each with its "(chance)" as shown.</summary>
    public static AssertingThat<StepCardSnapshot> Fires(this AssertingThat<StepCardSnapshot> that, WeaponPerk perk, params string[] lines) =>
        that.FiresFrom(perk.ToShownText(), lines);

    /// <summary><paramref name="catalyst"/> fired exactly <paramref name="lines"/>, in that order, each with its "(chance)" as shown.</summary>
    public static AssertingThat<StepCardSnapshot> Fires(this AssertingThat<StepCardSnapshot> that, Catalyst catalyst, params string[] lines) =>
        that.FiresFrom(catalyst.ToShownText(), lines);

    /// <summary><paramref name="mod"/> fired exactly <paramref name="lines"/>, in that order, each with its "(chance)" as shown.</summary>
    public static AssertingThat<StepCardSnapshot> Fires(this AssertingThat<StepCardSnapshot> that, ArmorMod mod, params string[] lines) =>
        that.FiresFrom(mod.ToShownText(), lines);

    /// <summary><paramref name="bonus"/> fired exactly <paramref name="lines"/>, in that order, each with its "(chance)" as shown.</summary>
    public static AssertingThat<StepCardSnapshot> Fires(this AssertingThat<StepCardSnapshot> that, ArmorSetBonus bonus, params string[] lines) =>
        that.FiresFrom(bonus.ToShownText(), lines);

    /// <summary><paramref name="armor"/> fired exactly <paramref name="lines"/>, in that order, each with its "(chance)" as shown.</summary>
    public static AssertingThat<StepCardSnapshot> Fires(this AssertingThat<StepCardSnapshot> that, ArmorPiece armor, params string[] lines) =>
        that.FiresFrom(armor.ToShownText(), lines);

    /// <summary><paramref name="ability"/> fired exactly <paramref name="lines"/>, in that order, each with its "(chance)" as shown.</summary>
    public static AssertingThat<StepCardSnapshot> Fires(this AssertingThat<StepCardSnapshot> that, Ability ability, params string[] lines) =>
        that.FiresFrom(ability.ToShownText(), lines);

    /// <summary><paramref name="perk"/> fired exactly <paramref name="lines"/>, in that order, each with its "(chance)" as shown.</summary>
    public static AssertingThat<StepCardSnapshot> Fires(this AssertingThat<StepCardSnapshot> that, ArtifactPerk perk, params string[] lines) =>
        that.FiresFrom(perk.ToShownText(), lines);

    /// <summary>
    /// Under the event that reads <paramref name="happened"/> ("↳ Threadling 🥊 hit"), <paramref name="perk"/> fired
    /// exactly <paramref name="lines"/>, in that order, each with its "(chance)" as shown.
    /// </summary>
    public static AssertingThat<StepCardSnapshot> FiresOn(this AssertingThat<StepCardSnapshot> that, string happened, ArtifactPerk perk, params string[] lines) =>
        that.FiresOn(happened, perk.ToShownText(), lines);

    /// <summary><paramref name="aspect"/> fired exactly <paramref name="lines"/>, in that order, each with its "(chance)" as shown.</summary>
    public static AssertingThat<StepCardSnapshot> Fires(this AssertingThat<StepCardSnapshot> that, Aspect aspect, params string[] lines) =>
        that.FiresFrom(aspect.ToShownText(), lines);

    /// <summary><paramref name="fragment"/> fired exactly <paramref name="lines"/>, in that order, each with its "(chance)" as shown.</summary>
    public static AssertingThat<StepCardSnapshot> Fires(this AssertingThat<StepCardSnapshot> that, Fragment fragment, params string[] lines) =>
        that.FiresFrom(fragment.ToShownText(), lines);

    /// <summary>
    /// Under the event that reads <paramref name="happened"/> ("📍 Picked up Ionic Trace"), <paramref name="fragment"/> fired
    /// exactly <paramref name="lines"/>, in that order, each with its "(chance)" as shown.
    /// </summary>
    public static AssertingThat<StepCardSnapshot> FiresOn(this AssertingThat<StepCardSnapshot> that, string happened, Fragment fragment, params string[] lines) =>
        that.FiresOn(happened, fragment.ToShownText(), lines);

    /// <summary><paramref name="keyword"/> fired exactly <paramref name="lines"/>, in that order, each with its "(chance)" as shown.</summary>
    public static AssertingThat<StepCardSnapshot> Fires(this AssertingThat<StepCardSnapshot> that, Keyword keyword, params string[] lines) =>
        that.FiresFrom(keyword.ToShownText(), lines);

    /// <summary>
    /// Under the event that reads <paramref name="happened"/> ("📍 Picked up Ionic Trace"), <paramref name="keyword"/> fired
    /// exactly <paramref name="lines"/>, in that order, each with its "(chance)" as shown.
    /// </summary>
    public static AssertingThat<StepCardSnapshot> FiresOn(this AssertingThat<StepCardSnapshot> that, string happened, Keyword keyword, params string[] lines) =>
        that.FiresOn(happened, keyword.ToShownText(), lines);

    private static AssertingThat<StepCardSnapshot> FiresOn(this AssertingThat<StepCardSnapshot> that, string happened, string name, string[] lines)
    {
        var events = that.InstanceToAssert.Events.Where(e => e.Text == happened).ToArray();
        Assert.True(events.Length == 1, $"The card reads {Describe(that.InstanceToAssert.Events)}, but one \"{happened}\" event was expected.");
        var shown = events[0].Sources.Where(source => source.Name == name).SelectMany(source => source.Lines).ToArray();
        Assert.True(shown.SequenceEqual(lines), $"On \"{happened}\", under {name} the card shows {Describe(shown)}, but {Describe(lines)} was expected.");
        return that;
    }

    /// <summary><paramref name="fragment"/>'s caveats are exactly <paramref name="caveats"/>, in that order ("· " left out).</summary>
    public static AssertingThat<StepCardSnapshot> HasCaveats(this AssertingThat<StepCardSnapshot> that, Fragment fragment, params string[] caveats)
    {
        var name = fragment.ToShownText();
        var shown = FindSources(that.InstanceToAssert, name).SelectMany(source => source.Caveats).Select(caveat => caveat.TrimStart('·', ' ')).ToArray();
        Assert.True(shown.SequenceEqual(caveats), $"Under {name} the card shows the caveats {Describe(shown)}, but {Describe(caveats)} was expected.");
        return that;
    }

    /// <summary>The card's damage resistance row <paramref name="row"/> ("All damage") reads <paramref name="pve"/> PvE and <paramref name="pvp"/> PvP.</summary>
    public static AssertingThat<StepCardSnapshot> HasResistance(this AssertingThat<StepCardSnapshot> that, string row, string pve, string pvp)
    {
        var shown = that.InstanceToAssert.Resistance.Rows.Where(found => found.Name == row).ToArray();
        Assert.True(shown.Length == 1, $"The card's damage resistance shows the rows {Describe(that.InstanceToAssert.Resistance.Rows.Select(found => found.Name))}, but one \"{row}\" row was expected.");
        Assert.True(
            shown[0].Pve == pve && shown[0].Pvp == pvp,
            $"The card's \"{row}\" resistance reads \"{shown[0].Pve}\" PvE and \"{shown[0].Pvp}\" PvP, but \"{pve}\" PvE and \"{pvp}\" PvP were expected.");
        return that;
    }

    /// <summary><paramref name="ability"/> is a source of the card's damage resistance, with the values <paramref name="values"/> ("90% · PvP 53%").</summary>
    public static AssertingThat<StepCardSnapshot> HasResistanceFrom(this AssertingThat<StepCardSnapshot> that, Ability ability, string values) =>
        that.HasResistanceFrom(ability.ToShownText(), values);

    /// <summary><paramref name="aspect"/> is a source of the card's damage resistance, with the values <paramref name="values"/> ("66% · PvP 32%").</summary>
    public static AssertingThat<StepCardSnapshot> HasResistanceFrom(this AssertingThat<StepCardSnapshot> that, Aspect aspect, string values) =>
        that.HasResistanceFrom(aspect.ToShownText(), values);

    /// <summary><paramref name="fragment"/> is a source of the card's damage resistance, with the values <paramref name="values"/> ("25% · PvP 10%").</summary>
    public static AssertingThat<StepCardSnapshot> HasResistanceFrom(this AssertingThat<StepCardSnapshot> that, Fragment fragment, string values) =>
        that.HasResistanceFrom(fragment.ToShownText(), values);

    /// <summary><paramref name="bonus"/> is a source of the card's damage resistance, with the values <paramref name="values"/> ("15% · PvP 0%").</summary>
    public static AssertingThat<StepCardSnapshot> HasResistanceFrom(this AssertingThat<StepCardSnapshot> that, ArmorSetBonus bonus, string values) =>
        that.HasResistanceFrom(bonus.ToShownText(), values);

    /// <summary><paramref name="keyword"/> is a source of the card's damage resistance, with the values <paramref name="values"/> ("15% · PvP 0%").</summary>
    public static AssertingThat<StepCardSnapshot> HasResistanceFrom(this AssertingThat<StepCardSnapshot> that, Keyword keyword, string values) =>
        that.HasResistanceFrom(keyword.ToShownText(), values);

    private static AssertingThat<StepCardSnapshot> HasResistanceFrom(this AssertingThat<StepCardSnapshot> that, string name, string values)
    {
        var sources = that.InstanceToAssert.Resistance.Sources;
        var shown = sources.Where(source => source.Name == name).Select(source => source.Value).ToArray();
        Assert.True(
            shown.Contains(values),
            $"The card's damage resistance comes from {Describe(sources.Select(source => $"{source.Name} {source.Value}"))}, but \"{name} {values}\" was expected among them.");
        return that;
    }

    /// <summary>The card's damage bonus row <paramref name="row"/> ("Melee") reads <paramref name="value"/> ("?").</summary>
    public static AssertingThat<StepCardSnapshot> HasDamageBonus(this AssertingThat<StepCardSnapshot> that, string row, string value)
    {
        var shown = that.InstanceToAssert.Bonus.Rows.Where(found => found.Name == row).ToArray();
        Assert.True(shown.Length == 1, $"The card's damage bonus shows the rows {Describe(that.InstanceToAssert.Bonus.Rows.Select(found => found.Name))}, but one \"{row}\" row was expected.");
        Assert.True(shown[0].Value == value, $"The card's \"{row}\" damage bonus reads \"{shown[0].Value}\", but \"{value}\" was expected.");
        return that;
    }

    /// <summary><paramref name="ability"/> is a source of the card's damage bonus, with the value <paramref name="value"/> ("?").</summary>
    public static AssertingThat<StepCardSnapshot> HasDamageBonusFrom(this AssertingThat<StepCardSnapshot> that, Ability ability, string value) =>
        that.HasDamageBonusFrom(ability.ToShownText(), value);

    /// <summary><paramref name="perk"/> is a source of the card's damage bonus, with the value <paramref name="value"/> ("+30%").</summary>
    public static AssertingThat<StepCardSnapshot> HasDamageBonusFrom(this AssertingThat<StepCardSnapshot> that, ArtifactPerk perk, string value) =>
        that.HasDamageBonusFrom(perk.ToShownText(), value);

    /// <summary><paramref name="armor"/> is a source of the card's damage bonus, with the value <paramref name="value"/> ("+100% [PvP 10%]").</summary>
    public static AssertingThat<StepCardSnapshot> HasDamageBonusFrom(this AssertingThat<StepCardSnapshot> that, ArmorPiece armor, string value) =>
        that.HasDamageBonusFrom(armor.ToShownText(), value);

    private static AssertingThat<StepCardSnapshot> HasDamageBonusFrom(this AssertingThat<StepCardSnapshot> that, string name, string value)
    {
        var sources = that.InstanceToAssert.Bonus.Sources;
        Assert.True(
            sources.Any(source => source.Name == name && source.Value == value),
            $"The card's damage bonus comes from {Describe(sources.Select(source => $"{source.Name} {source.Value}"))}, but \"{name} {value}\" was expected among them.");
        return that;
    }

    /// <summary>The card has no line from <paramref name="perk"/>.</summary>
    public static AssertingThat<StepCardSnapshot> DoesNotFire(this AssertingThat<StepCardSnapshot> that, WeaponPerk perk)
    {
        var name = perk.ToShownText();
        var fired = FindSources(that.InstanceToAssert, name);
        Assert.True(fired.IsEmpty, $"The card shows {name} with {Describe(fired.SelectMany(source => source.Lines))}, but no {name} line was expected.");
        return that;
    }

    /// <summary>The card has no line from <paramref name="armor"/>.</summary>
    public static AssertingThat<StepCardSnapshot> DoesNotFire(this AssertingThat<StepCardSnapshot> that, ArmorPiece armor)
    {
        var name = armor.ToShownText();
        var fired = FindSources(that.InstanceToAssert, name);
        Assert.True(fired.IsEmpty, $"The card shows {name} with {Describe(fired.SelectMany(source => source.Lines))}, but no {name} line was expected.");
        return that;
    }

    /// <summary>The card has no line from <paramref name="fragment"/>.</summary>
    public static AssertingThat<StepCardSnapshot> DoesNotFire(this AssertingThat<StepCardSnapshot> that, Fragment fragment)
    {
        var name = fragment.ToShownText();
        var fired = FindSources(that.InstanceToAssert, name);
        Assert.True(fired.IsEmpty, $"The card shows {name} with {Describe(fired.SelectMany(source => source.Lines))}, but no {name} line was expected.");
        return that;
    }

    /// <summary>The card has no line from <paramref name="bonus"/>.</summary>
    public static AssertingThat<StepCardSnapshot> DoesNotFire(this AssertingThat<StepCardSnapshot> that, ArmorSetBonus bonus)
    {
        var name = bonus.ToShownText();
        var fired = FindSources(that.InstanceToAssert, name);
        Assert.True(fired.IsEmpty, $"The card shows {name} with {Describe(fired.SelectMany(source => source.Lines))}, but no {name} line was expected.");
        return that;
    }

    /// <summary>No line of the card says "(chance)": everything it fired happens for sure.</summary>
    public static AssertingThat<StepCardSnapshot> HasNoChance(this AssertingThat<StepCardSnapshot> that)
    {
        var chances = that.InstanceToAssert.Events.SelectMany(e => e.Sources).SelectMany(s => s.Lines).Where(line => line.EndsWith(Chance, StringComparison.Ordinal)).ToArray();
        Assert.True(chances.Length == 0, $"The card shows {Describe(chances)}, but no line with \"{Chance}\" was expected.");
        return that;
    }

    /// <summary><paramref name="perk"/>'s "why:" reasons are exactly <paramref name="reasons"/>, in that order.</summary>
    public static AssertingThat<StepCardSnapshot> GivesReasons(this AssertingThat<StepCardSnapshot> that, WeaponPerk perk, params string[] reasons)
    {
        var name = perk.ToShownText();
        var shown = FindSources(that.InstanceToAssert, name).SelectMany(source => source.Reasons).ToArray();
        Assert.True(shown.SequenceEqual(reasons), $"Under {name} the card gives the reasons {Describe(shown)}, but {Describe(reasons)} was expected.");
        return that;
    }

    /// <summary>The state's <paramref name="group"/> shows exactly <paramref name="chips"/>, in that order.</summary>
    public static AssertingThat<StepCardSnapshot> Shows(this AssertingThat<StepCardSnapshot> that, StateGroup group, params string[] chips)
    {
        var shown = ReadGroup(that.InstanceToAssert.State, group).Select(chip => chip.Text).ToArray();
        Assert.True(shown.SequenceEqual(chips), $"The state's {group.ToShownText()} show {Describe(shown)}, but {Describe(chips)} was expected.");
        return that;
    }

    /// <summary>The state's <paramref name="group"/> reads "none".</summary>
    public static AssertingThat<StepCardSnapshot> ShowsNone(this AssertingThat<StepCardSnapshot> that, StateGroup group) =>
        that.Shows(group);

    private static AssertingThat<StepCardSnapshot> FiresFrom(this AssertingThat<StepCardSnapshot> that, string name, string[] lines)
    {
        var fired = FindSources(that.InstanceToAssert, name);
        Assert.True(!fired.IsEmpty, $"The card shows nothing from {name} (it shows {Describe(that.InstanceToAssert.Events)}), but {Describe(lines)} was expected.");
        var shown = fired.SelectMany(source => source.Lines).ToArray();
        Assert.True(shown.SequenceEqual(lines), $"Under {name} the card shows {Describe(shown)}, but {Describe(lines)} was expected.");
        return that;
    }

    private static ImmutableArray<FiredSource> FindSources(StepCardSnapshot card, string name) =>
        [.. card.Events.SelectMany(e => e.Sources).Where(source => source.Name == name)];

    private static ImmutableArray<StateChip> ReadGroup(StepState state, StateGroup group) =>
        group.Match(buffs => state.Buffs, weapons => state.Weapons, debuffs => state.Debuffs, ground => state.Ground, unlisted => []);

    private static string Describe(ImmutableArray<FiredEvent> events) =>
        events.IsEmpty ? "nothing" : string.Join("; ", events.Select(e => $"\"{e.Text}\": {string.Join(", ", e.Sources.Select(s => $"{s.Name} {Describe(s.Lines)}"))}"));

    private static string Describe(IEnumerable<string> texts)
    {
        var listed = string.Join(", ", texts.Select(text => $"\"{text}\""));
        return listed.Length == 0 ? "none" : listed;
    }
}
