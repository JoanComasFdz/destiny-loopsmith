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

    /// <summary>
    /// The tooltip: the glossary's facts ("up to x5 · 8s"), its own element's effects ("Energy weapon: +10 Range, …", "400%
    /// [PvP 200%] faster grenade and melee regeneration") and what it switches on ("switches on Spark of Frequency").
    /// </summary>
    public static string DescribeEntryTitle(StateEntry entry) =>
        string.Join(" · ", new[]
        {
            entry.Facts.UnwrapOr(""),
            string.Join(" · ", entry.OwnEffects),
            entry.SwitchesOn.IsEmpty ? "" : $"switches on {string.Join(", ", entry.SwitchesOn.Select(effect => effect.SourceName))}",
        }.Where(part => part.Length > 0));

    /// <summary>How many effects the status has right then: its own element's and the other elements' it switches on.</summary>
    public static int CountEffects(StateEntry entry) =>
        entry.OwnEffects.Length + entry.SwitchesOn.Length;

    /// <summary>"1 effect", "2 effects": its own (Stats for All's stats, Spark of Recharge's regeneration) and the others' it switches on right then.</summary>
    public static string DescribeSwitchesCount(StateEntry entry) =>
        CountEffects(entry) == 1 ? "1 effect" : $"{CountEffects(entry)} effects";

    /// <summary>"45% · PvP 25%"; a source against combatants only: "34.5% · PvP 0%" (it does nothing in PvP).</summary>
    public static string DescribeResistanceSource(ResistanceSource source) =>
        source.Pve.FormatPercent() + source.Pvp.Map(pvp => $" · PvP {pvp.FormatPercent()}").UnwrapOr(" · PvP 0%");

    /// <summary>A damage bonus row's values, each source's own, never added up: "?", "+50% · +10%".</summary>
    public static string DescribeDamageBonusLine(DamageBonusLine line) =>
        string.Join(" · ", line.Sources.Select(DescribeDamageBonus));

    /// <summary>"+20%", "+100% [PvP 10%]": one source's damage bonus, with its PvP value when the source gives one.</summary>
    public static string DescribeDamageBonus(DamageBonusSource source) =>
        source.Change.FormatSignedPercent() + DomainPhrasing.DescribePvp(source.Pvp);
}
