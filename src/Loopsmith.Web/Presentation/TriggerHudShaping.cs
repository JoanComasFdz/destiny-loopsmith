using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Web.Presentation;

/// <summary>
/// One element of the trigger HUD — the Super, an ability or a weapon — as the game's HUD shows it: its name, its icon
/// (the build's element or weapon, from the manifest excerpt; none: "?") and colour, and the palette row it acts as
/// (its hit / kill pair, or the class ability). <see cref="AtOnce"/>: its one action, when it has one and no target
/// count (the class ability, the finisher) — a tap acts at once. <see cref="IsNew"/>: one of its actions was unlocked by
/// the last step. <see cref="Secondaries"/>: the ability's secondary uses the build's rules name, shown below it.
/// </summary>
public sealed record HudSlot(
    TriggerChoice Choice, string Kind, string Name, Optional<string> Icon, Affinity Affinity, Optional<TriggerButton> AtOnce, bool IsNew,
    ImmutableArray<HudSecondary> Secondaries);

/// <summary>
/// A secondary use of an ability, below its square — a different trigger from the press (Tempest Strike's sliding melee,
/// Ascension's air move): its own slot (the palette row "cast:Melee:Slide", named "Sliding melee · Tempest Strike", the
/// icon of the element that names it), how the player does it ("Slide + melee"), its caption ("slide") and the elements
/// whose rules name it ("Tempest Strike").
/// </summary>
public sealed record HudSecondary(HudSlot Slot, string Gesture, string Caption, string NamedBy);

/// <summary>A weapon of the HUD: its slot, its damage type's icon and its archetype ("Grenade Launcher"), when known.</summary>
public sealed record HudWeapon(HudSlot Slot, WeaponSlot WeaponSlot, Optional<string> DamageIcon, Optional<string> Archetype);

/// <summary>A status the player can declare something about — its name, its colour — and those declarations ("at max (x10)", "ends").</summary>
public sealed record HudStatus(string Name, Affinity Affinity, ImmutableArray<TriggerButton> Buttons);

/// <summary>
/// The Next trigger section laid out like Destiny's HUD: the Super's diamond, then the grenade, melee and class ability
/// squares in the subclass's colour, each with its secondary uses below it (and the finisher's: the game has no HUD
/// square for it, so it shows the finisher's own icon — the player's equipped one after a Bungie login, else the
/// class's), then the weapons, each in its damage type's colour; above them, where the game
/// lists your buffs, the states to declare — the buffs on you, on your weapons and the debuffs on the pack (with a new
/// pack) — and what is on the ground to pick up.
/// </summary>
public sealed record TriggerHud(
    Affinity Subclass,
    Optional<HudSlot> Super,
    ImmutableArray<HudSlot> Abilities,
    Optional<HudSlot> Finisher,
    ImmutableArray<HudWeapon> Weapons,
    ImmutableArray<HudStatus> OnYou,
    ImmutableArray<HudStatus> OnWeapons,
    ImmutableArray<HudStatus> OnPack,
    ImmutableArray<TriggerButton> NewPack,
    ImmutableArray<TriggerButton> Ground);

/// <summary>
/// Pure: the palette's rows (<see cref="TriggerPaletteShaping.ShapePalette"/>) placed on the HUD, with the build's icons
/// (<see cref="LoadoutShaping"/>) and the finisher's (<see cref="LoopDesigning.FindFinisher"/>). Every row of the palette
/// has its place: no trigger is lost to the layout.
/// </summary>
public static class TriggerHudShaping
{
    public static TriggerHud ShapeHud(
        ImmutableArray<TriggerOption> options, ImmutableDictionary<string, TargetCount> counts, ValidatedBuild build, Optional<BungieProfile> profile)
    {
        var sections = TriggerPaletteShaping.ShapePalette(options, counts, build);
        var choices = sections.SelectMany(section => section.Choices).ToImmutableDictionary(choice => choice.Key);
        var abilities = build.Build.Abilities;
        var subclass = build.Build.Subclass.ToAffinity();
        var weapons = LoadoutShaping.ShapeLoadout(build, Optional.None<BungieProfile>()).Weapons;   // the HUD shows no tier or masterwork marks
        var declared = ListButtons(sections, TriggerGroup.Declare);
        var statuses = ShapeStatuses(build, declared);
        return new TriggerHud(
            subclass,
            ShapeAbility(build, choices, AbilityKind.Super, "Super", abilities.Super, subclass),
            [
                .. new[]
                {
                    ShapeAbility(build, choices, AbilityKind.Grenade, "Grenade", abilities.Grenade, subclass),
                    ShapeAbility(build, choices, AbilityKind.Melee, "Melee", abilities.Melee, subclass),
                    ShapeAbility(build, choices, AbilityKind.ClassAbility, "Class ability", abilities.ClassAbility, subclass),
                }.SelectMany(slot => slot.Match(found => new[] { found.Value }, _ => [])),
            ],
            ShapeFinisher(choices, LoopDesigning.FindFinisher(build, profile)),
            [.. weapons.SelectMany(weapon => ShapeWeapon(choices, weapon).Match(found => new[] { found.Value }, _ => []))],
            ListStatuses(statuses, KeywordKind.Buff),
            ListStatuses(statuses, KeywordKind.WeaponBuff),
            ListStatuses(statuses, KeywordKind.Debuff),
            [.. declared.Where(button => button.Action is PlayerAction.Declare { Declaration: StateDeclaration.NewPack })],
            ListButtons(sections, TriggerGroup.Pickup));
    }

    /// <summary>The choice the ability's press names, when the palette offers it (none: the build has no such trigger), with its secondary uses.</summary>
    private static Optional<HudSlot> ShapeAbility(
        ValidatedBuild build, ImmutableDictionary<string, TriggerChoice> choices, AbilityKind ability, string kind, Optional<ElementId> equipped, Affinity subclass)
    {
        var affinity = equipped.Bind(id => build.Catalog.Elements.TryGetValue(id, out var element) ? Optional.Some(element.Affinity) : Optional.None<Affinity>()).UnwrapOr(subclass);
        return choices.TryGetValue(TriggerPaletteShaping.ToAbilityKey(ability, AbilityInput.Press), out var choice)
            ? Optional.Some(new HudSlot(
                choice,
                kind,
                equipped.Bind(id => build.Catalog.Elements.TryGetValue(id, out var element) ? Optional.Some(element.Name) : Optional.None<string>()).UnwrapOr($"{kind} ?"),
                equipped.Bind(id => LoadoutShaping.FindElementIcon(build.Catalog, id)),
                affinity,
                FindAtOnce(choice),
                choice.Buttons.Any(button => button.IsNew),
                [.. LoopDesigning.ListSecondaryUses(build, ability).SelectMany(use => ShapeSecondary(build, choices, use, kind, affinity).Match(found => new[] { found.Value }, _ => []))]))
            : Optional.None<HudSlot>();
    }

    /// <summary>
    /// A secondary use's slot, when the palette offers it: named by its use and the elements that name it ("Sliding melee ·
    /// Tempest Strike"), with the first one's icon, in its ability's colour.
    /// </summary>
    private static Optional<HudSecondary> ShapeSecondary(
        ValidatedBuild build, ImmutableDictionary<string, TriggerChoice> choices, SecondaryUse use, string kind, Affinity affinity)
    {
        var named = string.Join(" + ", use.NamedBy.Select(id => build.Catalog.Elements.TryGetValue(id, out var element) ? element.Name : id.Value));
        var title = DomainPhrasing.Capitalize(use.Kind.DescribeAbilityUse(use.Input));
        return choices.TryGetValue(TriggerPaletteShaping.ToAbilityKey(use.Kind, use.Input), out var choice)
            ? Optional.Some(new HudSecondary(
                new HudSlot(
                    choice,
                    kind,
                    $"{title} · {named}",
                    use.NamedBy.Select(id => LoadoutShaping.FindElementIcon(build.Catalog, id)).FindFirstSome(),
                    affinity,
                    FindAtOnce(choice),
                    choice.Buttons.Any(button => button.IsNew),
                    []),
                DomainPhrasing.Capitalize(use.Kind.DescribeGesture(use.Input)),
                use.Input.ToInputWord(),
                named))
            : Optional.None<HudSecondary>();
    }

    /// <summary>
    /// The finisher's square, when the palette offers one: the finisher's name and icon (none known: "Finisher" and no
    /// icon), no element and no damage type (neutral); one tap does it.
    /// </summary>
    private static Optional<HudSlot> ShapeFinisher(ImmutableDictionary<string, TriggerChoice> choices, Optional<ManifestItem> finisher) =>
        choices.TryGetValue("finisher", out var choice)
            ? Optional.Some(new HudSlot(
                choice,
                "Finisher",
                finisher.Match(item => item.Value.Name, _ => choice.Title),
                finisher.Bind(item => item.Icon).Map(LoadoutShaping.ToIconUrl),
                Affinity.Neutral,
                FindAtOnce(choice),
                choice.Buttons.Any(button => button.IsNew),
                []))
            : Optional.None<HudSlot>();

    private static Optional<HudWeapon> ShapeWeapon(ImmutableDictionary<string, TriggerChoice> choices, WeaponView weapon) =>
        choices.TryGetValue($"fire:{weapon.Slot}", out var choice)
            ? Optional.Some(new HudWeapon(
                new HudSlot(choice, weapon.Slot.ToString(), weapon.Weapon.Name, weapon.Weapon.Icon, weapon.Type.ToAffinity(), FindAtOnce(choice), choice.Buttons.Any(button => button.IsNew), []),
                weapon.Slot,
                weapon.DamageIcon,
                weapon.Archetype.Map(DomainPhrasing.DescribeArchetype)))
            : Optional.None<HudWeapon>();

    /// <summary>A row's one action, when it has one and no target count: a tap on its slot acts at once.</summary>
    private static Optional<TriggerButton> FindAtOnce(TriggerChoice choice) =>
        choice.Buttons.Length == 1 && !choice.Targets.IsSome() ? Optional.Some(choice.Buttons[0]) : Optional.None<TriggerButton>();

    private static ImmutableArray<TriggerButton> ListButtons(ImmutableArray<TriggerSection> sections, TriggerGroup group) =>
        [.. sections.Where(section => section.Group == group).SelectMany(section => section.Choices).SelectMany(choice => choice.Buttons)];

    /// <summary>The declarations about a status (at max, ends), grouped by it in the palette's order, each captioned without its name ("at max (x10)").</summary>
    private static ImmutableArray<(KeywordKind Kind, HudStatus Status)> ShapeStatuses(ValidatedBuild build, ImmutableArray<TriggerButton> declared) =>
    [
        .. declared
            .SelectMany(button => ReadStatus(button.Action).Match(status => new[] { (Status: status.Value, Button: button) }, _ => []))
            .GroupBy(entry => entry.Status)
            .SelectMany(group => build.Catalog.Glossary.Statuses.TryGetValue(group.Key, out var definition)
                ? new[] { (definition.Kind, new HudStatus(definition.Name, definition.Affinity, [.. group.Select(entry => ToStatusButton(entry.Button, definition.Name))])) }
                : []),
    ];

    private static ImmutableArray<HudStatus> ListStatuses(ImmutableArray<(KeywordKind Kind, HudStatus Status)> statuses, KeywordKind kind) =>
        [.. statuses.Where(entry => entry.Kind == kind).Select(entry => entry.Status)];

    /// <summary>The status a declaration is about (none: a new pack, or not a declaration).</summary>
    private static Optional<StatusId> ReadStatus(PlayerAction action) =>
        action is PlayerAction.Declare declare
            ? declare.Declaration.Match(
                max => Optional.Some(max.Status),
                end => Optional.Some(end.Status),
                _ => Optional.None<StatusId>())
            : Optional.None<StatusId>();

    /// <summary>"Bolt Charge at max (x10)" → "at max (x10)": the button beside its status's name.</summary>
    private static TriggerButton ToStatusButton(TriggerButton button, string name) =>
        button with { Caption = button.Label.StartsWith(name + " ", StringComparison.Ordinal) ? button.Label[(name.Length + 1)..] : button.Label };
}
