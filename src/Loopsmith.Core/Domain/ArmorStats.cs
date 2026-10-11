using System.Collections.Immutable;
using Dunet;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Domain;

/// <summary>
/// What a plug that changes armor stats is, for adding up a build's stats from a player's own armor the way DIM does:
/// a piece's rolled stats (<see cref="Base"/>), its masterwork, its tuning (the empty tuning socket's plug marks the
/// socket), a stat mod, a subclass fragment.
/// </summary>
public enum StatPlugKind { Base, Masterwork, Tuning, Mod, Subclass }

/// <summary>One stat a plug changes; <see cref="Class"/>: only for that class (Spark of Focus lowers the class's own regeneration stat).</summary>
public sealed record PlugStat(StatKind Stat, int Value, Optional<GuardianClass> Class);

/// <summary>
/// A plug of the manifest excerpt's <c>statPlugs</c>. <see cref="OnlyEmptyStats"/>: it adds only to a piece's stats at
/// 0, the three its roll leaves empty (an Armor 3.0 masterwork, Balanced Tuning).
/// </summary>
public sealed record StatPlug(StatPlugKind Kind, ImmutableArray<PlugStat> Stats, bool OnlyEmptyStats);

/// <summary>Pure reads of a build's stat line by stat.</summary>
public static class StatReading
{
    /// <summary>The build's value of a stat ("?" when the build doesn't say).</summary>
    public static Optional<StatValue> ReadStat(this StatLine stats, StatKind stat) =>
        stat switch
        {
            StatKind.Weapons => stats.Weapons,
            StatKind.Health => stats.Health,
            StatKind.Class => stats.Class,
            StatKind.Grenade => stats.Grenade,
            StatKind.Super => stats.Super,
            _ => stats.Melee,
        };
}

/// <summary>Points in each armor stat: a piece's, a mod's, a build's. A part may be negative (a fragment's −10).</summary>
public sealed record StatPoints(int Weapons, int Health, int Class, int Grenade, int Super, int Melee);

/// <summary>A Bungie account's Destiny membership: its platform (Bungie's membership type number) and id.</summary>
public sealed record DestinyMembership(int Type, string Id);

/// <summary>Where a player's armor piece is: worn by a character, in a character's inventory, or in the vault.</summary>
[Union]
public partial record ArmorPlace
{
    partial record Equipped(GuardianClass Class);
    partial record Carried(GuardianClass Class);
    partial record Vault();
}

/// <summary>
/// An armor piece the player owns, as Bungie's profile has it: its instance, item hash, where it is, the plugs in
/// its sockets (enabled ones, socket by socket) and its energy capacity (Armor 2.0's masterwork reads it).
/// </summary>
public sealed record OwnedArmor(string InstanceId, ItemHash Hash, ArmorPlace Place, ImmutableArray<ItemHash> Plugs, Optional<int> EnergyCapacity);

/// <summary>A character's stats as Bungie has them right now: what it is wearing, with its mods and subclass.</summary>
public sealed record CharacterStats(GuardianClass Class, StatLine Stats);

/// <summary>What Loopsmith reads of a player's Bungie profile: their armor (characters' and vault's) and their characters' stats.</summary>
public sealed record BungieProfile(ImmutableArray<OwnedArmor> Armor, ImmutableArray<CharacterStats> Characters);

/// <summary>A piece the build wears (a DIM loadout's): its slot, name and the hashes that are it (an exotic's copies).</summary>
public sealed record ArmorPiece(ArmorSlot Slot, string Name, ImmutableArray<ItemHash> Hashes);

/// <summary>A copy of a piece the player owns, and the points it gives (its roll and masterwork); none when Loopsmith can't read its roll.</summary>
public sealed record PieceCopy(OwnedArmor Armor, Optional<StatPoints> Points);

/// <summary>A piece of the build and the player's copies of it (none: they don't own it).</summary>
public sealed record PieceChoice(ArmorPiece Piece, ImmutableArray<PieceCopy> Copies);

/// <summary>What the player can add the build's stats up from: a choice per piece the build wears, and their characters' current stats.</summary>
public sealed record StatChoices(ImmutableArray<PieceChoice> Pieces, ImmutableArray<CharacterStats> Characters);

/// <summary>One part of a build's stat total: the armor, a mod (and its copies), a fragment — its name and points.</summary>
public sealed record StatPart(string Source, StatPoints Points);

/// <summary>A build's stats added up from the player's armor, the parts they came from, and what was left out and why.</summary>
public sealed record StatTotals(StatLine Stats, ImmutableArray<StatPart> Parts, ImmutableArray<string> Notes);
