using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Core.BuildComposition;

/// <summary>
/// Pure: a build's weapons from the player's own copies (docs/loop-format.md, "Weapons from your account") or from every
/// weapon in the game ("Every item"), and a weapon slot emptied. A copy becomes a build weapon as a DIM loadout's does —
/// its slot, name, damage type and archetype from the manifest excerpt — with the perks it holds: in each trait column,
/// the plug of its sockets that column rolls with; its catalyst on when its socket holds it; and the copy it is (its
/// instance id). A weapon of the game is one with no copy: no roll (its perks are picked column by column) and its
/// catalyst off. A build wears one exotic weapon, and one weapon per slot. Which copies are the player's is
/// StatLoading's to say (<c>CopyFinding</c>).
/// </summary>
public static class WeaponSwapping
{
    /// <summary>The build wearing <paramref name="copy"/> where <paramref name="target"/> says: in place of one of its weapons, or added to an empty slot; or why it can't.</summary>
    public static Result<Build, string> SwapWeapon(RuleCatalog catalog, Build build, WeaponTarget target, OwnedWeapon copy) =>
        CheckWeapon(catalog, build, target, copy.Hash)
            .Bind(_ => ToWeapon(catalog, copy))
            .Map(worn => WearWeapon(build, target, worn));

    /// <summary>
    /// The build wearing the weapon <paramref name="hash"/> — a weapon of the game, picked from every weapon: no copy, so
    /// no roll and its catalyst off — where <paramref name="target"/> says; or why it can't (the excerpt must know it: a
    /// pick reads Bungie's manifest for it first).
    /// </summary>
    public static Result<Build, string> PutWeapon(RuleCatalog catalog, Build build, WeaponTarget target, ItemHash hash) =>
        CheckWeapon(catalog, build, target, hash)
            .Bind(_ => ToItemWeapon(catalog, hash, [], Optional.None<ItemInstanceId>()))
            .Map(worn => WearWeapon(build, target, worn));

    /// <summary>The build without its weapon number <paramref name="weapon"/> (that slot empty); or why it can't.</summary>
    public static Result<Build, string> RemoveWeapon(Build build, int weapon) =>
        weapon >= 0 && weapon < build.Weapons.Length
            ? new Result<Build, string>.Ok(build with { Weapons = build.Weapons.RemoveAt(weapon) })
            : Refuse<Build>($"The build has no weapon {weapon + 1}.");

    /// <summary>
    /// What "Every item" lists for where <paramref name="target"/> says: every weapon of <paramref name="listing"/> the
    /// build can wear there (that slot's; one exotic), the one it wears there first, then by name (a name's versions side
    /// by side), each with whether the rules know it; with a note on the exotic rule.
    /// </summary>
    public static WeaponItemOptions ListItemOptions(RuleCatalog catalog, Build build, ItemListing listing, WeaponTarget target)
    {
        var open = CheckTarget(build, target);
        var worn = build.FindTargetWeapon(target).Bind(weapon => weapon.Hash);
        var otherExotic = FindExoticElsewhere(catalog, build, target);
        return new WeaponItemOptions(
            build.FindTargetSlot(target),
            [
                .. open.Match(
                        slot => listing.Items.Where(item => CheckItem(slot.Value, otherExotic, Optional.Some(item)).IsOk()),
                        _ => [])
                    .Select(item => new WeaponItemOption(item, catalog.IsItemInRules(item), worn == Optional.Some(item.Hash)))
                    .OrderByDescending(option => option.Worn),
            ],
            [.. DescribeExoticRule(catalog, build, target).Match(note => new[] { note.Value }, _ => [])]);
    }

    private static Build WearWeapon(Build build, WeaponTarget target, WeaponLoadout worn) =>
        build with
        {
            Weapons = target.Match(inPlace => build.Weapons.SetItem(inPlace.Weapon, worn), _ => build.Weapons.Add(worn)),
        };

    /// <summary>
    /// Whether the build can wear the weapon <paramref name="hash"/> where <paramref name="target"/> says: a weapon the
    /// build has (in place of it) or a slot it has none in; a weapon the excerpt knows, for that slot, with a damage type;
    /// and an exotic only when the build's other weapons have none. Or why not.
    /// </summary>
    public static Result<Unit, string> CheckWeapon(RuleCatalog catalog, Build build, WeaponTarget target, ItemHash hash) =>
        CheckTarget(build, target).Bind(slot => CheckItem(slot, FindExoticElsewhere(catalog, build, target), catalog.Manifest.FindItem(hash)));

    /// <summary>
    /// Whether <paramref name="item"/> (of the excerpt, or of the listing) can go in <paramref name="slot"/>: a weapon of
    /// that slot with a damage type, and an exotic only when <paramref name="otherExotic"/> (the build's exotic elsewhere)
    /// is none. Or why not.
    /// </summary>
    private static Result<Unit, string> CheckItem(WeaponSlot slot, Optional<string> otherExotic, Optional<ManifestItem> item)
    {
        var kind = item.Bind(found => found.Kind is ManifestKind.Weapon known ? Optional.Some(known) : Optional.None<ManifestKind.Weapon>());
        var name = item.Match(found => found.Value.Name, _ => "That weapon");
        var exotic = item.Match(found => found.Value.Tier == Optional.Some(ItemTier.Exotic), _ => false);
        return kind.Match(
            known => known.Value.Slot != slot
                ? Refuse($"{name} is a {known.Value.Slot.DescribeWeaponSlot().ToLowerInvariant()} weapon: it can't go in the {slot.DescribeWeaponSlot().ToLowerInvariant()} slot.")
                : !known.Value.DamageType.IsSome()
                    ? Refuse($"Loopsmith doesn't know {name}'s damage type, so it can't put it in the build.")
                    : exotic && otherExotic.IsSome()
                        ? Refuse($"A build wears one exotic weapon: swap {otherExotic.UnwrapOr("its exotic")} for a legendary weapon first.")
                        : new Result<Unit, string>.Ok(new Unit.Value()),
            _ => Refuse("Loopsmith doesn't know that weapon yet (Bungie's manifest wasn't read for it), so it can't put it in the build."));
    }

    /// <summary>What the weapon picker says when the build's exotic weapon is another one (no exotic can be picked here); none when it isn't.</summary>
    public static Optional<string> DescribeExoticRule(RuleCatalog catalog, Build build, WeaponTarget target) =>
        FindExoticElsewhere(catalog, build, target).Map(name => $"A build wears one exotic weapon: swap {name} for a legendary weapon to pick an exotic here.");

    /// <summary>
    /// A copy as a build weapon: the excerpt's slot, name, damage type and archetype; its perks (each trait column's plug
    /// of its sockets); its catalyst on when its socket holds the catalyst; and its instance id. Or why it can't be one.
    /// </summary>
    public static Result<WeaponLoadout, string> ToWeapon(RuleCatalog catalog, OwnedWeapon copy) =>
        ToItemWeapon(catalog, copy.Hash, copy.Plugs, Optional.Some(copy.InstanceId));

    /// <summary>The weapon <paramref name="hash"/> as a build weapon, holding <paramref name="plugs"/> (none: no roll, its catalyst off), as the copy <paramref name="instance"/> when it is one.</summary>
    private static Result<WeaponLoadout, string> ToItemWeapon(RuleCatalog catalog, ItemHash hash, IEnumerable<ItemHash> plugs, Optional<ItemInstanceId> instance) =>
        catalog.Manifest.FindItem(hash).Match(
            item => item.Value.Kind is ManifestKind.Weapon { DamageType: Optional<DamageType>.Some type } kind
                ? new Result<WeaponLoadout, string>.Ok(new WeaponLoadout(
                    kind.Slot,
                    item.Value.Name,
                    type.Value,
                    item.Value.Type.Bind(ManifestReading.ToArchetype),
                    [],
                    Optional.Some(hash),
                    kind.Traits.ReadRoll(plugs),
                    kind.HoldsCatalyst(plugs),
                    instance))
                : Refuse<WeaponLoadout>($"Loopsmith doesn't know {item.Value.Name} as a weapon with a damage type, so it can't put it in the build."),
            _ => Refuse<WeaponLoadout>("Loopsmith doesn't know that weapon yet (Bungie's manifest wasn't read for it), so it can't put it in the build."));

    /// <summary>
    /// The build with each of its weapons in <paramref name="copies"/> (by weapon number: the player's copy of it) holding
    /// that copy's perks and catalyst; which weapons were loaded (a copy Loopsmith could read) and which of those changed.
    /// </summary>
    public static (Build Build, ImmutableArray<int> Loaded, ImmutableArray<int> Changed) LoadCopies(RuleCatalog catalog, Build build, ImmutableArray<(int Weapon, OwnedWeapon Copy)> copies)
    {
        var read = copies
            .Where(entry => entry.Weapon >= 0 && entry.Weapon < build.Weapons.Length)
            .SelectMany(entry => ToWeapon(catalog, entry.Copy).Match(worn => new[] { (entry.Weapon, Worn: worn.Value) }, _ => []))
            .ToImmutableArray();
        var loaded = read.Aggregate(build.Weapons, (weapons, entry) => weapons.SetItem(entry.Weapon, weapons[entry.Weapon] with { Roll = entry.Worn.Roll, Catalyst = entry.Worn.Catalyst }));
        var changed = read
            .Where(entry => !loaded[entry.Weapon].Roll.SequenceEqual(build.Weapons[entry.Weapon].Roll) || loaded[entry.Weapon].Catalyst != build.Weapons[entry.Weapon].Catalyst)
            .Select(entry => entry.Weapon)
            .ToImmutableArray();
        return (build with { Weapons = loaded }, [.. read.Select(entry => entry.Weapon)], changed);
    }

    /// <summary>The slot <paramref name="target"/> fills, when it is one the build can take a weapon in (its weapon's; an empty one); or why not.</summary>
    private static Result<WeaponSlot, string> CheckTarget(Build build, WeaponTarget target) =>
        target.Match(
            inPlace => build.FindTargetWeapon(inPlace).Match(
                weapon => new Result<WeaponSlot, string>.Ok(weapon.Value.Slot),
                _ => Refuse<WeaponSlot>($"The build has no weapon {inPlace.Weapon + 1}.")),
            empty => build.Weapons.Any(weapon => weapon.Slot == empty.Slot)
                ? Refuse<WeaponSlot>($"The build already has a {empty.Slot.DescribeWeaponSlot().ToLowerInvariant()} weapon: pick one in its place.")
                : new Result<WeaponSlot, string>.Ok(empty.Slot));

    /// <summary>
    /// The name of the build's exotic weapon, when it is another than the one <paramref name="target"/> replaces: by its
    /// manifest item's rarity, or — a hand-written weapon with no hash — by an exotic weapon element among its perks.
    /// </summary>
    private static Optional<string> FindExoticElsewhere(RuleCatalog catalog, Build build, WeaponTarget target) =>
        build.Weapons
            .Select((worn, index) => (worn, index))
            .Where(entry => target != new WeaponTarget.InPlaceOf(entry.index))
            .Where(entry => IsExotic(catalog, entry.worn))
            .Select(entry => Optional.Some(entry.worn.Name))
            .FindFirstSome();

    private static bool IsExotic(RuleCatalog catalog, WeaponLoadout weapon) =>
        weapon.Hash.Bind(catalog.Manifest.FindItem).Match(item => item.Value.Tier == Optional.Some(ItemTier.Exotic), _ => false)
        || weapon.Perks.Any(perk => catalog.Elements.TryGetValue(perk, out var element) && element.Kind == ElementKind.ExoticWeapon);

    private static Result<Unit, string> Refuse(string why) => new Result<Unit, string>.Error(why);

    private static Result<T, string> Refuse<T>(string why) => new Result<T, string>.Error(why);
}
