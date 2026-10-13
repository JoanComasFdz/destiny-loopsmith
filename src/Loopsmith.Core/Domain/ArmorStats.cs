using System.Collections.Immutable;
using Dunet;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Domain;

/// <summary>One stat a plug changes; <see cref="Class"/>: only for that class (Spark of Focus lowers the class's own regeneration stat).</summary>
public sealed record PlugStat(StatKind Stat, int Value, Optional<GuardianClass> Class);

/// <summary>
/// A plug of the manifest excerpt's <c>statPlugs</c>: what it does to armor stats, for adding up a build's stats from a
/// player's own armor the way DIM does.
/// </summary>
[Union]
public partial record StatPlug
{
    /// <summary>A piece's rolled stats: an armor-stat plug, or its intrinsic (Armor 3.0's archetype).</summary>
    partial record Rolled(ImmutableArray<PlugStat> Stats);

    /// <summary>An Armor 3.0 masterwork: it adds only to the piece's stats at 0, the three its roll leaves empty.</summary>
    partial record Masterwork(ImmutableArray<PlugStat> Stats);

    /// <summary>The empty tuning socket's plug: it marks a piece that can take a tuning mod and changes no stat.</summary>
    partial record TuningSocket();

    /// <summary>A tuning mod that moves points between two stats (+5 Grenade, −5 Melee).</summary>
    partial record Tuning(ImmutableArray<PlugStat> Stats);

    /// <summary>Balanced Tuning: like a masterwork, only to a piece's stats at 0; one piece per tuning socket.</summary>
    partial record BalancedTuning(ImmutableArray<PlugStat> Stats);

    /// <summary>A stat mod (+10 Grenade).</summary>
    partial record StatMod(ImmutableArray<PlugStat> Stats);

    /// <summary>A fragment with stats (Spark of Shock's −10 Grenade).</summary>
    partial record Fragment(ImmutableArray<PlugStat> Stats);
}

/// <summary>Pure reads of a build's stat line by stat.</summary>
public static class StatReading
{
    /// <summary>The six armor stats in the order the game lists them (Health first, Weapons last).</summary>
    public static readonly ImmutableArray<StatKind> GameOrder =
        [StatKind.Health, StatKind.Melee, StatKind.Grenade, StatKind.Super, StatKind.Class, StatKind.Weapons];

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

    /// <summary>The points in a stat.</summary>
    public static int ReadPoints(this StatPoints points, StatKind stat) =>
        stat switch
        {
            StatKind.Weapons => points.Weapons,
            StatKind.Health => points.Health,
            StatKind.Class => points.Class,
            StatKind.Grenade => points.Grenade,
            StatKind.Super => points.Super,
            _ => points.Melee,
        };
}

/// <summary>Points in each armor stat: a piece's, a mod's, a build's. A part may be negative (a fragment's −10).</summary>
public sealed record StatPoints(int Weapons, int Health, int Class, int Grenade, int Super, int Melee);

/// <summary>A Bungie account's Destiny membership: its platform (Bungie's membership type) and id.</summary>
public sealed record DestinyMembership(MembershipType Type, MembershipId Id);

/// <summary>Where a player's armor piece (or weapon) is: worn by a character, in a character's inventory, or in the vault.</summary>
[Union]
public partial record ArmorPlace
{
    partial record Equipped(GuardianClass Class);
    partial record Carried(GuardianClass Class);
    partial record Vault();
}

/// <summary>
/// An armor piece the player owns, as Bungie's profile has it: its instance, item hash, where it is, the plugs in
/// its sockets (enabled ones, socket by socket), its energy capacity (Armor 2.0's masterwork reads it) and its marks.
/// </summary>
public sealed record OwnedArmor(ItemInstanceId InstanceId, ItemHash Hash, ArmorPlace Place, ImmutableArray<ItemHash> Plugs, Optional<int> EnergyCapacity, GearMarks Marks);

/// <summary>
/// What the game draws on a copy's icon, as DIM does: its gear tier (none for an item without one), whether it is
/// masterworked, the breaker it has as Bungie gives it for that copy, when any, and its power level.
/// </summary>
public sealed record GearMarks(Optional<GearTier> Tier, bool Masterworked, Optional<ItemHash> Breaker, Optional<PowerLevel> Power);

/// <summary>
/// A weapon the player owns: its instance, item hash, where it is, its marks, the plugs in its sockets (enabled ones,
/// socket by socket: its perks, its catalyst) and each socket's plugs it has — the one in it, then those it can switch
/// to (Bungie's reusable plugs: a perk column that rolled two perks or more has each of them).
/// </summary>
public sealed record OwnedWeapon(ItemInstanceId InstanceId, ItemHash Hash, ArmorPlace Place, GearMarks Marks, ImmutableArray<ItemHash> Plugs, ImmutableArray<ImmutableArray<ItemHash>> Sockets);

/// <summary>The finisher a character of the player's has equipped (its Finishers slot): the character's class and the item.</summary>
public sealed record EquippedFinisher(GuardianClass Class, ItemHash Hash);

/// <summary>
/// What Loopsmith reads of a player's Bungie profile: their armor and weapons (worn, carried and in the vault), and the
/// finisher each character has equipped.
/// </summary>
public sealed record BungieProfile(ImmutableArray<OwnedArmor> Armor, ImmutableArray<OwnedWeapon> Weapons, ImmutableArray<EquippedFinisher> Finishers);

/// <summary>A piece the build wears (a DIM loadout's): its slot, name and the hashes that are it (an exotic's copies).</summary>
public sealed record ArmorPiece(ArmorSlot Slot, string Name, ImmutableArray<ItemHash> Hashes);

/// <summary>A copy of a piece the player owns, and the points it gives (its roll and masterwork); none when Loopsmith can't read its roll.</summary>
public sealed record PieceCopy(OwnedArmor Armor, Optional<StatPoints> Points);

/// <summary>A piece of the build and the player's copies of it (none: they don't own it).</summary>
public sealed record PieceChoice(ArmorPiece Piece, ImmutableArray<PieceCopy> Copies);

/// <summary>What the player can add the build's stats up from: a choice per piece the build wears.</summary>
public sealed record StatChoices(ImmutableArray<PieceChoice> Pieces);

/// <summary>
/// A piece the player can pick for a slot of the build: their copy, its manifest item, the points it gives (none:
/// Loopsmith can't read its roll) and whether it is the copy the build wears.
/// </summary>
public sealed record ArmorOption(OwnedArmor Armor, ManifestItem Item, Optional<StatPoints> Points, bool Worn);

/// <summary>What the armor picker lists for a slot (the copy the build wears first), and what it leaves out and why.</summary>
public sealed record ArmorOptions(ArmorSlot Slot, ImmutableArray<ArmorOption> Options, ImmutableArray<string> Notes);

/// <summary>
/// One of the player's weapons the weapon picker lists: the copy, its manifest item, the perks it holds (each trait
/// column's plug of its sockets: what the build would hold), the perks it has in each trait column (the one it holds
/// first, then those it can switch to), whether its catalyst is in, and whether the build wears it.
/// </summary>
public sealed record WeaponOption(OwnedWeapon Weapon, ManifestItem Item, ImmutableArray<Optional<ItemHash>> Roll, ImmutableArray<ImmutableArray<ItemHash>> Columns, bool Catalyst, bool Worn);

/// <summary>What the weapon picker lists for a slot (none: a weapon number the build hasn't), the copy the build names first, and what it leaves out and why.</summary>
public sealed record WeaponOptions(Optional<WeaponSlot> Slot, ImmutableArray<WeaponOption> Options, ImmutableArray<string> Notes);

/// <summary>A weapon of the game the weapon picker's "Every item" lists: its manifest item (the listing's) and whether the build wears it there.</summary>
public sealed record WeaponItemOption(ManifestItem Item, bool Worn);

/// <summary>What "Every item" lists for a weapon slot (none: a weapon number the build hasn't), the weapon the build wears there first, and what it leaves out and why.</summary>
public sealed record WeaponItemOptions(Optional<WeaponSlot> Slot, ImmutableArray<WeaponItemOption> Options, ImmutableArray<string> Notes);

/// <summary>
/// An armor piece of the game the armor picker's "Every item" lists: its manifest item (the listing's), its armor set
/// when it is of one, whether the build wears it, and what the build can't hold of it (an exotic class item's spirits).
/// </summary>
public sealed record ArmorItemOption(ManifestItem Item, Optional<ArmorSet> Set, bool Worn, Optional<string> Note);

/// <summary>What "Every item" lists for an armor slot (the piece the build wears there first), and what it leaves out and why.</summary>
public sealed record ArmorItemOptions(ArmorSlot Slot, ImmutableArray<ArmorItemOption> Options, ImmutableArray<string> Notes);

/// <summary>
/// Whether the copies a build wears (a dim.gg share's, by instance id) are the logged-in player's: all of them, some
/// (the pieces whose copy isn't in their account), or none — someone else's loadout, or one that doesn't say.
/// </summary>
[Union]
public partial record LoadoutOwnership
{
    partial record Own();
    partial record PartlyOwn(ImmutableArray<string> Missing);
    partial record NotOwn();
}

/// <summary>One part of a build's stat total: an armor piece, a mod (and its copies), a fragment — its name and points.</summary>
public sealed record StatPart(string Source, StatPoints Points);

/// <summary>A build's stats added up from the player's armor, the parts they came from, and what was left out and why.</summary>
public sealed record StatTotals(StatLine Stats, ImmutableArray<StatPart> Parts, ImmutableArray<string> Notes);
