using System.Collections.Immutable;
using Loopsmith.Core.Causality;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Simulation;

/// <summary>
/// Pure: the enemy counts a step's ability or weapon offers (ADRs D4), each with the equipped rules that need that many
/// from it in one action — Arc Siphon's two kills with an Arc weapon, Synergy's twelve with its weapon — so the designer
/// says what a count is for.
/// </summary>
public static class TargetCountListing
{
    /// <summary>The plain counts every ability and weapon offers.</summary>
    public const int PlainCounts = 9;

    /// <summary>
    /// 1 to 9, each with the elements whose rules need that many from this action's ability or weapon, then every larger
    /// count such a rule needs (Synergy's 12). A rule of another ability or weapon names nothing here.
    /// </summary>
    public static ImmutableArray<TargetCountChoice> ListTargetCountChoices(ValidatedBuild build, PlayerAction action)
    {
        var origin = FindActionOrigin(build, action);
        var superType = build.ReadSuperType();
        var named = build.Equipped
            .SelectMany(equipped => equipped.Element.Rules.SelectMany(rule => ReadNamedCount(rule.On)
                .Where(count => origin.Match(found => count.Via.IsMatchedBy(found.Value, superType), _ => false))
                .Select(count => (count.AtLeast, count.Within, equipped.Element.Name))))
            .ToImmutableArray();
        var counts = Enumerable.Range(1, PlainCounts)
            .Concat(named.Select(count => count.AtLeast.Value))
            .Distinct()
            .Order();
        return
        [
            .. counts.Select(value => new TargetCountChoice(
                TargetCount.From(value),
                [.. named.Where(count => count.AtLeast.Value == value).Select(count => new CountNeed(count.Name, count.Within)).Distinct()])),
        ];
    }

    /// <summary>
    /// What an ability or weapon action hits with: the ability (of the subclass's element, by its use) or the weapon in
    /// that slot; none for any other step, or a slot with no weapon.
    /// </summary>
    public static Optional<DamageOrigin> FindActionOrigin(ValidatedBuild build, PlayerAction action) =>
        action.Match(
            cast => Optional.Some<DamageOrigin>(new DamageOrigin.Ability(cast.Kind.ToAbilityKind(), build.Build.Subclass.ToDamageType(), cast.Input)),
            _ => Optional.None<DamageOrigin>(),
            fire => build.Build.Weapons
                .Where(weapon => weapon.Slot == fire.Slot)
                .Select(weapon => Optional.Some<DamageOrigin>(new DamageOrigin.Weapon(fire.Slot, weapon.Type)))
                .FindFirstSome(),
            _ => Optional.None<DamageOrigin>(),
            _ => Optional.None<DamageOrigin>(),
            _ => Optional.None<DamageOrigin>());

    private sealed record NamedCount(DamageSource Via, TargetCount AtLeast, Optional<Seconds> Within);

    /// <summary>A kill or hit of at least N in one action names N; "fewer than N" names nothing (1 to 9 reach it).</summary>
    private static IEnumerable<NamedCount> ReadNamedCount(Trigger trigger) =>
        trigger switch
        {
            Trigger.KillMultiple kill => [new NamedCount(kill.Via, kill.AtLeast, kill.Within)],
            Trigger.DamageMultiple damage => [new NamedCount(damage.Via, damage.AtLeast, Optional.None<Seconds>())],
            _ => [],
        };
}
