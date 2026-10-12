using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.BuildComposition;

/// <summary>
/// Pure: what a picker without a login lists of the game's weapons and armor (ManifestJoining lists them) for a slot of
/// the build: the items the build can wear there (WeaponSwapping, ArmorSwapping: the slot, the class, one exotic), each
/// with whether the rules know it and whether the build wears it, and the note on the exotic rule.
/// </summary>
public static class GameItemPicking
{
    /// <summary>The game's weapons for where <paramref name="target"/> says (a weapon of the build, or an empty slot).</summary>
    public static GameItemOptions ListWeapons(RuleCatalog catalog, Build build, GameItems game, WeaponTarget target)
    {
        var worn = build.FindTargetWeapon(target).Bind(weapon => weapon.Hash);
        var exotic = WeaponSwapping.DescribeExoticRule(catalog, build, target);
        return new GameItemOptions(
            [
                .. game.Weapons
                    .Where(item => WeaponSwapping.CheckWeaponItem(catalog, build, target, Optional.Some(item)).IsOk())
                    .Select(item => new GameItemOption(item, IsKnown(catalog, item), worn == Optional.Some(item.Hash))),
            ],
            [.. exotic.Match(note => new[] { note.Value }, _ => [])]);
    }

    /// <summary>The game's armor pieces for a slot (the build's class, or any class).</summary>
    public static GameItemOptions ListPieces(RuleCatalog catalog, Build build, GameItems game, ArmorSlot slot)
    {
        var exotic = ArmorSwapping.DescribeExoticRule(catalog, build, slot);
        return new GameItemOptions(
            [
                .. game.Armor
                    .Where(item => ArmorSwapping.CheckPieceItem(catalog, build, slot, Optional.Some(item)).IsOk())
                    .Select(item => new GameItemOption(item, IsKnown(catalog, item), ArmorSwapping.IsWorn(catalog, build, item.Hash))),
            ],
            [.. exotic.Match(note => new[] { note.Value }, _ => [])]);
    }

    /// <summary>An exotic counts as known when the rules have it (an exotic weapon's intrinsic perk is its element); a legendary always does.</summary>
    private static bool IsKnown(RuleCatalog catalog, ManifestItem item) =>
        item.Tier != Optional.Some(ItemTier.Exotic)
        || catalog.FindElementByHash(item.Hash).IsSome()
        || (item.Kind is ManifestKind.Weapon { Intrinsic: Optional<ItemHash>.Some intrinsic } && catalog.FindElementByHash(intrinsic.Value).IsSome());
}
