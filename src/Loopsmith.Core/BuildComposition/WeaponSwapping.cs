using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Core.BuildComposition;

/// <summary>
/// Pure: a build's weapons from the player's own copies (docs/loop-format.md, "Weapons from your account"). A copy
/// becomes a build weapon as a DIM loadout's does — its slot, name, damage type and archetype from the manifest
/// excerpt — with the perks it holds: in each trait column, the plug of its sockets that column rolls with; its
/// catalyst on when its socket holds it; and the copy it is (its instance id). A build wears one exotic weapon.
/// </summary>
public static class WeaponSwapping
{
    /// <summary>
    /// The build wearing <paramref name="copy"/> in <paramref name="slot"/>: in place of its weapon number
    /// <paramref name="weapon"/> (0-based, in the build's order), or added when none is given (an empty slot); or why it can't.
    /// </summary>
    public static Result<Build, string> SwapWeapon(RuleCatalog catalog, Build build, WeaponSlot slot, Optional<int> weapon, OwnedWeapon copy) =>
        CheckWeapon(catalog, build, slot, weapon, copy.Hash)
            .Bind(_ => ToWeapon(catalog, copy))
            .Map(worn => build with
            {
                Weapons = weapon.Match(index => build.Weapons.SetItem(index.Value, worn), _ => build.Weapons.Add(worn)),
            });

    /// <summary>
    /// Whether the build can wear the weapon <paramref name="hash"/> in <paramref name="slot"/> (in place of its weapon
    /// <paramref name="weapon"/>, or added): one the excerpt knows, for that slot, and an exotic only when the build's
    /// other weapons have none; or why not.
    /// </summary>
    public static Result<Unit, string> CheckWeapon(RuleCatalog catalog, Build build, WeaponSlot slot, Optional<int> weapon, ItemHash hash)
    {
        var item = catalog.Manifest.FindItem(hash);
        var kind = item.Bind(found => found.Kind is ManifestKind.Weapon known ? Optional.Some(known) : Optional.None<ManifestKind.Weapon>());
        var name = item.Match(found => found.Value.Name, _ => "That weapon");
        var exotic = item.Match(found => found.Value.Tier == Optional.Some(ItemTier.Exotic), _ => false);
        var otherExotic = FindExoticElsewhere(catalog, build, weapon);
        return weapon.Match(index => index.Value >= 0 && index.Value < build.Weapons.Length, _ => true)
            ? kind.Match(
                known => known.Value.Slot != slot
                    ? Refuse($"{name} is a {known.Value.Slot.DescribeWeaponSlot().ToLowerInvariant()} weapon, not a {slot.DescribeWeaponSlot().ToLowerInvariant()} one.")
                    : !known.Value.DamageType.IsSome()
                        ? Refuse($"Loopsmith doesn't know {name}'s damage type, so it can't put it in the build.")
                        : exotic && otherExotic.IsSome()
                            ? Refuse($"A build wears one exotic weapon: swap {otherExotic.UnwrapOr("its exotic")} for a legendary weapon first.")
                            : new Result<Unit, string>.Ok(new Unit.Value()),
                _ => Refuse("Loopsmith doesn't know that weapon yet (Bungie's manifest wasn't read for it), so it can't put it in the build."))
            : Refuse($"The build has no weapon {weapon.Match(index => index.Value + 1, _ => 0)}.");
    }

    /// <summary>What the weapon picker says when the build's exotic weapon is another one (no exotic can be picked here); none when it isn't.</summary>
    public static Optional<string> DescribeExoticRule(RuleCatalog catalog, Build build, Optional<int> weapon) =>
        FindExoticElsewhere(catalog, build, weapon).Map(name => $"A build wears one exotic weapon: swap {name} for a legendary weapon to pick an exotic here.");

    /// <summary>
    /// A copy as a build weapon: the excerpt's slot, name, damage type and archetype; its perks (each trait column's plug
    /// of its sockets); its catalyst on when its socket holds the catalyst; and its instance id. Or why it can't be one.
    /// </summary>
    public static Result<WeaponLoadout, string> ToWeapon(RuleCatalog catalog, OwnedWeapon copy) =>
        catalog.Manifest.FindItem(copy.Hash).Match(
            item => item.Value.Kind is ManifestKind.Weapon { DamageType: Optional<DamageType>.Some type } kind
                ? new Result<WeaponLoadout, string>.Ok(new WeaponLoadout(
                    kind.Slot,
                    item.Value.Name,
                    type.Value,
                    item.Value.Type.Bind(ManifestReading.ToArchetype),
                    [],
                    Optional.Some(copy.Hash),
                    kind.Traits.ReadRoll(copy.Plugs),
                    HoldsCatalyst(kind, copy),
                    Optional.Some(copy.InstanceId)))
                : Refuse<WeaponLoadout>($"Loopsmith doesn't know {item.Value.Name} as a weapon with a damage type, so it can't put it in the build."),
            _ => Refuse<WeaponLoadout>("Loopsmith doesn't know that weapon yet (Bungie's manifest wasn't read for it), so it can't put it in the build."));

    /// <summary>
    /// The build with each weapon whose copy is the player's (its instance id in <paramref name="profile"/>) holding that
    /// copy's perks and catalyst, and the indices of the weapons that changed; a weapon whose copy isn't theirs stays as it is.
    /// </summary>
    public static (Build Build, ImmutableArray<int> Changed) LoadCopies(RuleCatalog catalog, Build build, BungieProfile profile)
    {
        var loaded = build.Weapons.Select(weapon => LoadCopy(catalog, profile, weapon)).ToImmutableArray();
        var changed = Enumerable.Range(0, build.Weapons.Length)
            .Where(index => !loaded[index].Roll.SequenceEqual(build.Weapons[index].Roll) || loaded[index].Catalyst != build.Weapons[index].Catalyst)
            .ToImmutableArray();
        return (build with { Weapons = loaded }, changed);
    }

    /// <summary>A build weapon holding its copy's perks and catalyst when the copy is the player's; else as it is.</summary>
    private static WeaponLoadout LoadCopy(RuleCatalog catalog, BungieProfile profile, WeaponLoadout weapon) =>
        FindCopy(profile, weapon)
            .Bind(copy => ToWeapon(catalog, copy).Match(worn => Optional.Some(worn.Value), _ => Optional.None<WeaponLoadout>()))
            .Match(worn => weapon with { Roll = worn.Value.Roll, Catalyst = worn.Value.Catalyst }, _ => weapon);

    /// <summary>The names of the build's weapons whose copy is the player's, and of those that name a copy that isn't (in the build's order).</summary>
    public static (ImmutableArray<string> Theirs, ImmutableArray<string> NotTheirs) SortCopies(Build build, BungieProfile profile) =>
        (
            [.. build.Weapons.Where(weapon => FindCopy(profile, weapon).IsSome()).Select(weapon => weapon.Name)],
            [.. build.Weapons.Where(weapon => !FindCopy(profile, weapon).IsSome()).Select(weapon => weapon.Name)]
        );

    /// <summary>
    /// What the login says of the build's weapons: those whose copy is theirs hold its perks now; for the others, ⋯
    /// picks one of theirs. Empty for a build without weapons.
    /// </summary>
    public static string DescribeCopies(ImmutableArray<string> theirs, ImmutableArray<string> notTheirs) =>
        (theirs.IsEmpty, notTheirs.IsEmpty) switch
        {
            (true, true) => "",
            (false, true) => "Its weapons are yours too, with their perks.",
            (false, false) => $"{JoinWithAnd(theirs)} {(theirs.Length == 1 ? "is" : "are")} yours too, with {(theirs.Length == 1 ? "its" : "their")} perks; tap ⋯ next to {JoinWithAnd(notTheirs)} to pick one of yours.",
            (true, false) => "Tap ⋯ next to a weapon to pick one of yours.",
        };

    private static string JoinWithAnd(ImmutableArray<string> parts) =>
        parts.Length <= 1 ? string.Concat(parts) : $"{string.Join(", ", parts[..^1])} and {parts[^1]}";

    /// <summary>The player's copy a build weapon names (its instance id), when it is in their account.</summary>
    private static Optional<OwnedWeapon> FindCopy(BungieProfile profile, WeaponLoadout weapon) =>
        weapon.Instance.Bind(instance => profile.Weapons.Where(owned => owned.InstanceId == instance).Select(Optional.Some).FindFirstSome());

    /// <summary>True when the copy's sockets hold its exotic catalyst (a catalyst completed and inserted).</summary>
    private static bool HoldsCatalyst(ManifestKind.Weapon kind, OwnedWeapon copy) =>
        kind.Catalyst.Match(catalyst => copy.Plugs.Contains(catalyst.Value), _ => false);

    /// <summary>The name of the build's exotic weapon when it is another weapon than number <paramref name="weapon"/> (any, when none is given).</summary>
    private static Optional<string> FindExoticElsewhere(RuleCatalog catalog, Build build, Optional<int> weapon) =>
        build.Weapons
            .Select((worn, index) => (worn, index))
            .Where(entry => weapon != Optional.Some(entry.index))
            .Where(entry => entry.worn.Hash.Bind(catalog.Manifest.FindItem).Match(item => item.Value.Tier == Optional.Some(ItemTier.Exotic), _ => false))
            .Select(entry => Optional.Some(entry.worn.Name))
            .FindFirstSome();

    private static Result<Unit, string> Refuse(string why) => new Result<Unit, string>.Error(why);

    private static Result<T, string> Refuse<T>(string why) => new Result<T, string>.Error(why);
}
