using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.Phrasing;
using Loopsmith.Core.TraceRendering;

namespace Loopsmith.Web.Presentation;

/// <summary>A weapon of the build a state entry is about: its name and icon (Slice: Festival Flight).</summary>
public sealed record BuffWeapon(string Name, Optional<string> Icon);

/// <summary>
/// Pure: what a step's state draws next to its entries — the build's weapons whose own perks give a weapon buff (Core
/// says which), what tapping an entry opens, its tooltip and how many effects it switches on.
/// </summary>
public static class StateHudShaping
{
    public static ImmutableArray<BuffWeapon> ListBuffWeapons(ValidatedBuild build, StateEntry entry) =>
        entry.Keyword is KeywordRef.Status status
            ? ListWeapons(build, LoopDesigning.ListWeaponsGiving(build, status.Id))
            : [];

    /// <summary>The build's weapons at these places, with their icons.</summary>
    public static ImmutableArray<BuffWeapon> ListWeapons(ValidatedBuild build, ImmutableArray<int> indices) =>
    [
        .. indices
            .Select(index => build.Build.Weapons[index])
            .Select(weapon => new BuffWeapon(
                weapon.Name,
                weapon.Hash.Bind(hash => build.Catalog.Manifest.FindItem(hash)).Bind(item => item.Icon).Map(LoadoutShaping.ToIconUrl))),
    ];

    /// <summary>Its element's details; a status without one, the status's own (its facts, what it switches on); a pickup without one, none.</summary>
    public static Optional<ItemDetails> FindEntryDetails(StateEntry entry) =>
        entry.Element.Match(
            element => Optional.Some<ItemDetails>(new ItemDetails.OfElement(element.Value)),
            _ => entry.Keyword is KeywordRef.Status status
                ? Optional.Some<ItemDetails>(new ItemDetails.OfStatus(status.Id))
                : Optional.None<ItemDetails>());

    /// <summary>The tooltip: the glossary's facts ("up to x5 · 8s") and what it switches on ("switches on Spark of Frequency").</summary>
    public static string DescribeEntryTitle(StateEntry entry) =>
        string.Join(" · ", new[]
        {
            entry.Facts.UnwrapOr(""),
            entry.SwitchesOn.IsEmpty ? "" : $"switches on {string.Join(", ", entry.SwitchesOn.Select(effect => effect.SourceName))}",
        }.Where(part => part.Length > 0));

    /// <summary>"1 effect", "2 effects": how many other elements' effects the status switches on right then.</summary>
    public static string DescribeSwitchesCount(StateEntry entry) =>
        entry.SwitchesOn.Length == 1 ? "1 effect" : $"{entry.SwitchesOn.Length} effects";

    /// <summary>"All damage", or what the line's resistance is against, as a heading ("Arc combatants").</summary>
    public static string DescribeResistanceLine(ResistanceLine line) =>
        line.Against.Map(CapitalizeFirst).UnwrapOr("All damage");

    /// <summary>The PvP total; "—" for a line against combatants only (it does nothing in PvP).</summary>
    public static string DescribeResistancePvp(ResistanceLine line) =>
        line.Pvp.Map(pvp => pvp.FormatPercent()).UnwrapOr("—");

    /// <summary>"All damage", or "Arc combatants: all damage ×" — a line against some damage only adds its sources to all damage's.</summary>
    public static string DescribeResistanceSourcesHeading(ResistanceLine line) =>
        line.Against.Map(against => $"{CapitalizeFirst(against)}: all damage ×").UnwrapOr("All damage");

    /// <summary>"45% · PvP 25%"; a source against combatants only: "34.5% · not in PvP".</summary>
    public static string DescribeResistanceSource(ResistanceSource source) =>
        source.Pve.FormatPercent() + source.Pvp.Map(pvp => $" · PvP {pvp.FormatPercent()}").UnwrapOr(" · not in PvP");

    private static string CapitalizeFirst(string text) =>
        text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
