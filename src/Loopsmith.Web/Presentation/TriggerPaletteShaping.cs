using System.Collections.Immutable;
using System.Globalization;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Web.Presentation;

/// <summary>
/// One button of the palette: the action it appends (the option aimed at its row's target count), that action's token
/// and label in the core's wording ("grenade:kill:3", "Grenade (💀 kill 3)"), and the button's short caption ("🥊 hit", "💀 kill").
/// <see cref="StandsApart"/>: the row's target count and option don't apply to it (a weapon's reload, ADRs D37, and its
/// Ready step, D44), so it stands at the row's right end, away from them. <see cref="IsEnabled"/>: false for a button shown
/// though no rule of the build reacts to it (a Ready step without Mechaneer's Tricksleeves).
/// </summary>
public sealed record TriggerButton(PlayerAction Action, string Token, string Label, string Caption, bool IsNew, bool StandsApart, bool IsEnabled = true);

/// <summary>
/// A row of the palette: one trigger, or the hit / kill pair of the same ability use or weapon (a weapon's reload too), or the
/// Special and Heavy ammo bricks ("ammo", ADRs D43). <see cref="Key"/> names
/// the row ("cast:Grenade", "cast:Melee:Slide", "fire:Kinetic", "class", "class:Air", …) and stays the same from one step
/// to the next.
/// <see cref="Targets"/> is how many enemies the row's actions hit or kill — none for a row without targets (class
/// ability, pickups, declared states); <see cref="ListTargetCountOptions"/> says which counts it offers.
/// <see cref="Options"/> are the options its actions can carry in this build, and whether each is on (ADRs D36).
/// </summary>
public sealed record TriggerChoice(string Key, string Title, Optional<TargetCount> Targets, ImmutableArray<TriggerButton> Buttons, ImmutableArray<TriggerOptionToggle> Options);

/// <summary>
/// An option the player turns on or off for a row's actions (ADRs D36): a weapon's "🛡 shield" (the step breaks a
/// combatant's shield) and "🎯 precision" (its hits and kills are precision ones), an ability's "⬜ Spend fully" (the use
/// spends its last charge), an ammo brick's "🛝 by sliding" (picked up by sliding over it, ADRs D43); <see cref="Hint"/> says what it is for.
/// </summary>
public sealed record TriggerOptionToggle(StepOption Option, string Caption, string Hint, bool IsOn);

/// <summary>A count a row offers and its text: what it is for when an equipped rule needs it ("12 · Synergy").</summary>
public sealed record TargetCountOption(int Value, string Text);

public sealed record TriggerSection(TriggerGroup Group, ImmutableArray<TriggerChoice> Choices);

/// <summary>
/// Pure: <see cref="LoopDesigning.ListTriggerOptions"/> → palette sections with hit/kill pairs side by side, each pair
/// aimed at the target count picked for its row (<see cref="LoopDesigning.SetTargetCount"/>). Every option is pickable.
/// </summary>
public static class TriggerPaletteShaping
{
    /// <summary>
    /// The enemy counts a row offers from the state the next step starts in: up to the last that still fills something
    /// (Bolt Charge to its cap, the grenade's energy), every count an equipped rule needs from its ability or weapon ("kill
    /// 12 with this weapon": Synergy's counter full) and the row's own count, each saying what it is for.
    /// </summary>
    public static ImmutableArray<TargetCountOption> ListTargetCountOptions(ValidatedBuild build, GameState state, PlayerAction action) =>
    [
        .. LoopDesigning.ListTargetCountChoices(build, state, action)
            .Select(choice => new TargetCountOption(choice.Count.Value, build.Catalog.Glossary.DescribeTargetCountChoice(choice))),
    ];

    /// <summary>The action a row's counts are listed for: its kill when it has one (a kill hits too, so it fills the most).</summary>
    public static PlayerAction FindCountedAction(TriggerChoice choice) =>
        choice.Buttons
            .Select(button => button.Action)
            .Where(action => action is PlayerAction.CastAbility { Hit: HitOutcome.Kill } or PlayerAction.FireWeapon { Hit: HitOutcome.Kill })
            .DefaultIfEmpty(choice.Buttons[0].Action)
            .First();

    /// <param name="counts">The target count picked per row <see cref="TriggerChoice.Key"/>; a row not in it aims at one enemy.</param>
    /// <param name="optionsOn">The options the player turned on, each as its row's <see cref="TriggerChoice.Key"/> and the option (<see cref="ToOptionKey"/>).</param>
    public static ImmutableArray<TriggerSection> ShapePalette(
        ImmutableArray<TriggerOption> options, ImmutableDictionary<string, TargetCount> counts, ImmutableHashSet<string> optionsOn, ValidatedBuild build) =>
    [
        .. options
            .GroupBy(option => option.Group)
            .OrderBy(group => group.Key)
            .Select(group => new TriggerSection(group.Key, ShapeChoices([.. group], counts, optionsOn, build))),
    ];

    /// <summary>A picked count ("3") as a <see cref="TargetCount"/>; none when it is not a whole number in range.</summary>
    public static Optional<TargetCount> ParseTargetCount(string? text) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && TargetCount.TryFrom(value, out var count)
            ? Optional.Some(count)
            : Optional.None<TargetCount>();

    private static ImmutableArray<TriggerChoice> ShapeChoices(
        ImmutableArray<TriggerOption> options, ImmutableDictionary<string, TargetCount> counts, ImmutableHashSet<string> optionsOn, ValidatedBuild build) =>
    [
        .. options
            .GroupBy(option => ToPairKey(option.Action))
            .Select(pair => ShapeChoice(pair.Key, [.. pair.OrderBy(option => ReadHitOrder(option.Action))], counts, optionsOn, build)),
    ];

    private static TriggerChoice ShapeChoice(
        string key, ImmutableArray<TriggerOption> pair, ImmutableDictionary<string, TargetCount> counts, ImmutableHashSet<string> optionsOn, ValidatedBuild build)
    {
        var targets = LoopDesigning.ReadTargetCount(pair[0].Action)
            .Map(one => counts.TryGetValue(key, out var picked) ? picked : one);
        var toggles = LoopDesigning.ListStepOptions(build, pair[0].Action)
            .Select(option => ShapeToggle(option, optionsOn.Contains(ToOptionKey(key, option))))
            .ToImmutableArray();
        var title = pair[0].Action is PlayerAction.PickUpAmmo ? DomainPhrasing.AmmoBrick : pair.Length == 1 ? pair[0].Label : ReadTitle(pair[0].Label);
        var buttons = pair
            .Select(option => ShapeButton(option, targets, toggles, ReadCaption(option, pair.Length), build))
            .ToImmutableArray();
        return new TriggerChoice(key, title, targets, buttons, toggles);
    }

    /// <summary>The ammo bricks' row (ADRs D43): its key and its title.</summary>
    public const string AmmoBrickKey = "ammo";

    /// <summary>An option of a row, as the player turns it on: "fire:Energy:Precision".</summary>
    public static string ToOptionKey(string rowKey, StepOption option) => $"{rowKey}:{option}";

    /// <summary>A button's caption: "🥊 hit", "💀 kill", "🔄 reload", an ammo brick's "special" or "heavy" in a row of several; its whole label alone.</summary>
    private static string ReadCaption(TriggerOption option, int buttons) =>
        (option.Action, buttons) switch
        {
            (PlayerAction.ReloadWeapon, _) => DomainPhrasing.ReloadCaption,
            (PlayerAction.PickUpAmmo ammo, _) => ammo.Brick.ToString().ToLowerInvariant(),
            (PlayerAction.ReadyWeapon, _) => DomainPhrasing.ReadyWeaponCaption,
            (_, 1) => option.Label,
            _ => ReadQualifier(option.Label),
        };

    private static TriggerOptionToggle ShapeToggle(StepOption option, bool isOn) =>
        option switch
        {
            StepOption.BreaksShield => new TriggerOptionToggle(option, DomainPhrasing.ShieldOption, "This hit or kill breaks a combatant's shield, with damage of its element", isOn),
            StepOption.Precision => new TriggerOptionToggle(option, DomainPhrasing.PrecisionOption, "This hit or kill lands on the enemy's weak spot: a precision hit or kill", isOn),
            StepOption.Slide => new TriggerOptionToggle(option, DomainPhrasing.SlideOption, "You pick the brick up by sliding over it", isOn),
            _ => new TriggerOptionToggle(option, DomainPhrasing.LastChargeBox, "This use spends the ability's last charge: its energy is fully expended", isOn),
        };

    private static TriggerButton ShapeButton(TriggerOption option, Optional<TargetCount> targets, ImmutableArray<TriggerOptionToggle> toggles, string caption, ValidatedBuild build)
    {
        var aimed = targets.Match(count => LoopDesigning.SetTargetCount(option.Action, count.Value), _ => option.Action);
        var action = toggles.Aggregate(aimed, (step, toggle) => LoopDesigning.SetStepOption(step, toggle.Option, toggle.IsOn));
        return new TriggerButton(action, action.ToActionToken(), build.DescribeAction(action), caption, option.IsNew, action is PlayerAction.ReloadWeapon or PlayerAction.ReadyWeapon, option.IsEnabled);
    }

    /// <summary>
    /// The row key of an ability's use: "cast:Grenade", "class" for a press; a secondary use is a row of its own
    /// ("cast:Melee:Slide", "class:Air") — a different trigger, doing different things.
    /// </summary>
    public static string ToAbilityKey(AbilityKind kind, AbilityInput input)
    {
        var press = kind == AbilityKind.ClassAbility ? "class" : $"cast:{kind}";
        return input == AbilityInput.Press ? press : $"{press}:{input}";
    }

    /// <summary>Hit and kill of the same ability use or weapon share a key, and a weapon's reload its; the two ammo bricks share one; everything else stands alone.</summary>
    private static string ToPairKey(PlayerAction action) =>
        action.Match(
            cast => ToAbilityKey(cast.Kind.ToAbilityKind(), cast.Input),
            use => ToAbilityKey(AbilityKind.ClassAbility, use.Input),
            fire => $"fire:{fire.Slot}",
            collect => $"pickup:{collect.Pickup}",
            declare => ((PlayerAction)declare).ToActionToken(),
            _ => "finisher",
            reload => $"fire:{reload.Slot}",
            _ => AmmoBrickKey,
            ready => $"fire:{ready.Slot}");

    private static int ReadHitOrder(PlayerAction action) =>
        action.Match(
            cast => cast.Hit == HitOutcome.Kill ? 1 : 0,
            _ => 0,
            fire => fire.Hit == HitOutcome.Kill ? 1 : 0,
            _ => 0,
            _ => 0,
            _ => 0,
            _ => 3,   // a reload stays at the row's right end
            ammo => ammo.Brick == AmmoBrick.Special ? 0 : 1,
            _ => 2);   // a Ready step, before the reload

    /// <summary>"Grenade (💀 kill)" → "Grenade".</summary>
    private static string ReadTitle(string label)
    {
        var open = label.LastIndexOf(" (", StringComparison.Ordinal);
        return open < 0 ? label : label[..open];
    }

    /// <summary>"Grenade (💀 kill)" → "💀 kill".</summary>
    private static string ReadQualifier(string label)
    {
        var open = label.LastIndexOf(" (", StringComparison.Ordinal);
        return open < 0 || !label.EndsWith(')') ? label : label[(open + 2)..^1];
    }
}
