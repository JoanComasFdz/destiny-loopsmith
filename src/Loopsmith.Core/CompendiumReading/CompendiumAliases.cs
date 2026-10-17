using System.Collections.Immutable;
using Loopsmith.Core.Domain;

namespace Loopsmith.Core.CompendiumReading;

/// <summary>
/// How a rule element's name finds its Compendium entry when the two differ (ADRs D12), from
/// <c>tools/compendium/aliases.yaml</c>: a tab's entries drop a start (<see cref="Prefixes"/>: "Spark of "), an end
/// (<see cref="Suffixes"/>: " (enhanced)") or some words (<see cref="DroppedWords"/>: one Armor Mods row for every
/// element's Siphon) of the element's name; <see cref="Names"/> gives an element's entry name outright;
/// <see cref="Worded"/> are numbers a row says in words ("fully restores" for 100%); <see cref="AlternativeBrackets"/> the
/// sections whose <c>[…]</c> is another PvE value, not the PvP one. Each entry says why.
/// </summary>
public sealed record CompendiumAliases(
    ImmutableArray<NameAffix> Prefixes,
    ImmutableArray<NameAffix> Suffixes,
    ImmutableArray<DroppedWords> DroppedWords,
    ImmutableArray<EntryName> Names,
    ImmutableArray<WordedNumber> Worded,
    ImmutableArray<AlternativeBrackets> AlternativeBrackets);

/// <summary>In <see cref="Tab"/>'s <see cref="Section"/>, <c>[…]</c> holds another PvE value (Arc's grenades: with Spark of Magnitude).</summary>
public sealed record AlternativeBrackets(string Tab, string Section, string Why);

/// <summary>In <see cref="Tab"/>, an entry's name lacks <see cref="Text"/> at the start (or end) of the element's.</summary>
public sealed record NameAffix(string Tab, string Text, string Why);

/// <summary>In <see cref="Tab"/>, an entry's name lacks these words of the element's ("Arc Siphon" → "Siphon").</summary>
public sealed record DroppedWords(string Tab, ImmutableArray<string> Words, string Why);

/// <summary>The element's entry is named <see cref="Name"/> (Melee Damage Resistance is on the "Resistance" row).</summary>
public sealed record EntryName(ElementId Element, string Name, string Why);

/// <summary>The element's <see cref="Value"/> (as the rules hold it: 100% is 1) is a number its row says in words.</summary>
public sealed record WordedNumber(ElementId Element, decimal Value, string Why);
