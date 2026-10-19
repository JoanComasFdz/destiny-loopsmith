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

    /// <summary><paramref name="mod"/> fired exactly <paramref name="lines"/>, in that order, each with its "(chance)" as shown.</summary>
    public static AssertingThat<StepCardSnapshot> Fires(this AssertingThat<StepCardSnapshot> that, ArmorMod mod, params string[] lines) =>
        that.FiresFrom(mod.ToShownText(), lines);

    /// <summary><paramref name="ability"/> fired exactly <paramref name="lines"/>, in that order, each with its "(chance)" as shown.</summary>
    public static AssertingThat<StepCardSnapshot> Fires(this AssertingThat<StepCardSnapshot> that, Ability ability, params string[] lines) =>
        that.FiresFrom(ability.ToShownText(), lines);

    /// <summary><paramref name="aspect"/> fired exactly <paramref name="lines"/>, in that order, each with its "(chance)" as shown.</summary>
    public static AssertingThat<StepCardSnapshot> Fires(this AssertingThat<StepCardSnapshot> that, Aspect aspect, params string[] lines) =>
        that.FiresFrom(aspect.ToShownText(), lines);

    /// <summary><paramref name="fragment"/> fired exactly <paramref name="lines"/>, in that order, each with its "(chance)" as shown.</summary>
    public static AssertingThat<StepCardSnapshot> Fires(this AssertingThat<StepCardSnapshot> that, Fragment fragment, params string[] lines) =>
        that.FiresFrom(fragment.ToShownText(), lines);

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

    private static AssertingThat<StepCardSnapshot> HasResistanceFrom(this AssertingThat<StepCardSnapshot> that, string name, string values)
    {
        var sources = that.InstanceToAssert.Resistance.Sources;
        var shown = sources.Where(source => source.Name == name).Select(source => source.Value).ToArray();
        Assert.True(
            shown.Contains(values),
            $"The card's damage resistance comes from {Describe(sources.Select(source => $"{source.Name} {source.Value}"))}, but \"{name} {values}\" was expected among them.");
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

    /// <summary>The chip <paramref name="chip"/> of the state's <paramref name="group"/> has the tooltip <paramref name="tooltip"/>.</summary>
    public static AssertingThat<StepCardSnapshot> HasTooltip(this AssertingThat<StepCardSnapshot> that, StateGroup group, string chip, string tooltip)
    {
        var shown = ReadGroup(that.InstanceToAssert.State, group).Where(found => found.Text == chip).Select(found => found.Tooltip).ToArray();
        Assert.True(shown.Length > 0, $"The state's {group.ToShownText()} show no \"{chip}\" chip, but one with the tooltip \"{tooltip}\" was expected.");
        Assert.True(shown.All(found => found == tooltip), $"The \"{chip}\" chip's tooltip is {Describe(shown)}, but \"{tooltip}\" was expected.");
        return that;
    }

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
