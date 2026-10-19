using System.Globalization;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Phrasing;

/// <summary>
/// Kernel: the textual form of a player action, read back (inverse of <c>DomainPhrasing.ToActionToken</c>).
/// Read by the loop-file parser, so a loop file takes exactly the tokens the designer writes.
/// </summary>
public static class ActionTokenParsing
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public const string Grammar =
        "grenade[:hold]|melee[:slide|air]|super[:air|combo|guard][:hit|kill[:N]] (grenade and melee [:last]), class[:air][:last], kinetic|energy|power[:hit|kill[:N]][:shield][:precision], a kill or a finisher [:elite|miniboss|champion|boss], kinetic|energy|power:reload, kinetic|energy|power:ready, ammo:special|heavy[:slide], finisher, pickup:<id>, max:<status>, end:<status>, pack:new, health:critical, health:hit";

    /// <summary>
    /// <c>grenade</c>, <c>grenade:kill</c>, <c>grenade:kill:3</c>, <c>kinetic:hit:5</c>, <c>class</c>, <c>class:air</c> (an air move
    /// that spends the class ability, like Ascension), a secondary ability's own token — <c>melee:slide:kill</c> (Tempest
    /// Strike's uppercut), <c>melee:air</c>, <c>grenade:hold</c>, the Super's <c>super:air</c>, <c>super:combo</c> and
    /// <c>super:guard</c> (Arc Staff's heavy attack in the air, Palm Strike, the guard) — <c>finisher</c> (a finisher on one enemy
    /// of the pack: it kills it),
    /// <c>pickup:orb-of-power</c>, and the states the player declares (ADRs D3): <c>max:bolt-charge</c>, <c>end:amplified</c>,
    /// <c>pack:new</c> (the next enemies are a new pack, ADRs D7), <c>health:critical</c> (you reached critical health),
    /// <c>health:hit</c> (you are damaged while at critical health).
    /// Without <c>:kill</c> the hit only damages; without a count it hits one enemy. The count is validated here, at the
    /// boundary (<see cref="TargetCount"/>: 1..20); whether a declaration holds is the engine's (a blocked step).
    /// An option goes last (ADRs D36): <c>:last</c>, the use that spends the grenade's, melee's or class ability's last
    /// charge (<c>grenade:kill:last</c>, <c>class:last</c>); <c>:shield</c>, a weapon step that breaks a combatant's shield
    /// (<c>kinetic:kill:3:shield</c>); <c>:precision</c>, a weapon step whose hits and kills are precision ones
    /// (<c>energy:kill:precision</c>), with <c>:shield</c> too (<c>energy:kill:shield:precision</c>, in either order); and the rank
    /// of a stronger enemy than the pack's minors that a kill step or a finisher kills (<c>energy:kill:elite</c>,
    /// <c>grenade:kill:3:last:boss</c>, <c>finisher:champion</c>), with the other options, in any order.
    /// <c>kinetic:reload</c> reloads that weapon (ADRs D37): like <c>kinetic:ready</c>, which readies it (ADRs D44), it takes no count or option. <c>ammo:special</c> and
    /// <c>ammo:heavy</c> pick up an ammo brick of that ammo, <c>ammo:heavy:slide</c> by sliding over it (Spark of Momentum,
    /// ADRs D42).
    /// </summary>
    public static Result<PlayerAction, string> ParseActionToken(string token)
    {
        var all = token.Trim().ToLowerInvariant().Split(':');
        var options = ReadOptions(all);
        var ranks = options.Where(IsRankWord).ToArray();
        var kind = ranks.Length > 1 ? OptionKind.Invalid : ClassifyOptions([.. options.Where(word => !IsRankWord(word))]);
        var rank = ranks.Length == 1 ? ToRank(ranks[0]) : EnemyTier.Minor;
        var charge = options.Contains("last") ? ChargeUse.Last : ChargeUse.Plain;
        var shield = options.Contains("shield") ? ShieldBreak.Breaks : ShieldBreak.None;
        var zone = options.Contains("precision") ? HitZone.Precision : HitZone.Body;
        var parts = all[..^options.Length];
        var action = (parts[0], kind) switch
        {
            (_, OptionKind.Invalid) => FailUnknown(token),
            ("grenade", not OptionKind.Weapon) => ParseCast(token, parts, OffensiveAbility.Grenade, charge),
            ("melee", not OptionKind.Weapon) => ParseCast(token, parts, OffensiveAbility.Melee, charge),
            ("super", OptionKind.None) => ParseCast(token, parts, OffensiveAbility.Super, charge),
            ("class", not OptionKind.Weapon) when parts.Length == 1 => Ok(new PlayerAction.UseClassAbility(AbilityInput.Press, charge)),
            ("class", not OptionKind.Weapon) when parts.Length == 2 && FindSecondaryInput(AbilityKind.ClassAbility, parts[1]) is Optional<AbilityInput>.Some input =>
                Ok(new PlayerAction.UseClassAbility(input.Value, charge)),
            (_, OptionKind.None) when parts.Length == 2 && parts[1] == "reload" && FindWeaponSlot(parts[0]) is Optional<WeaponSlot>.Some slot =>
                Ok(new PlayerAction.ReloadWeapon(slot.Value)),
            (_, OptionKind.None) when parts.Length == 2 && parts[1] == "ready" && FindWeaponSlot(parts[0]) is Optional<WeaponSlot>.Some readied =>
                Ok(new PlayerAction.ReadyWeapon(readied.Value)),
            ("kinetic", not OptionKind.Charge) => ParseAimedAction(token, parts, (hit, targets) => new PlayerAction.FireWeapon(WeaponSlot.Kinetic, hit, targets, shield, zone)),
            ("energy", not OptionKind.Charge) => ParseAimedAction(token, parts, (hit, targets) => new PlayerAction.FireWeapon(WeaponSlot.Energy, hit, targets, shield, zone)),
            ("power", not OptionKind.Charge) => ParseAimedAction(token, parts, (hit, targets) => new PlayerAction.FireWeapon(WeaponSlot.Power, hit, targets, shield, zone)),
            (_, not OptionKind.None) => FailUnknown(token),
            ("finisher", _) when parts.Length == 1 => Ok(new PlayerAction.PerformFinisher()),
            ("ammo", _) when parts.Length is 2 or 3 && FindAmmoBrick(parts[1]) is Optional<AmmoBrick>.Some brick && (parts.Length == 2 || parts[2] == "slide") =>
                Ok(new PlayerAction.PickUpAmmo(brick.Value, parts.Length == 3 ? BrickPickup.Slide : BrickPickup.Plain)),
            ("pickup", _) when parts.Length == 2 && PickupId.TryFrom(parts[1], out var pickup) => Ok(new PlayerAction.CollectPickups(pickup)),
            ("max", _) when parts.Length == 2 && StatusId.TryFrom(parts[1], out var maxed) =>
                Ok(new PlayerAction.Declare(new StateDeclaration.ReachMax(maxed))),
            ("end", _) when parts.Length == 2 && StatusId.TryFrom(parts[1], out var ended) =>
                Ok(new PlayerAction.Declare(new StateDeclaration.EndStatus(ended))),
            ("pack", _) when parts.Length == 2 && parts[1] == "new" => Ok(new PlayerAction.Declare(new StateDeclaration.NewPack())),
            ("health", _) when parts.Length == 2 && parts[1] == "critical" => Ok(new PlayerAction.Declare(new StateDeclaration.CriticalHealth())),
            ("health", _) when parts.Length == 2 && parts[1] == "hit" => Ok(new PlayerAction.Declare(new StateDeclaration.HitAtCriticalHealth())),
            _ => FailUnknown(token),
        };
        return action.Bind(read => SetRank(token, read, rank));
    }

    /// <summary>
    /// The rank goes with a kill: an ability's or weapon's kill step, or a finisher (it kills); a hit, a class ability, a
    /// reload or a declaration takes none (<c>energy:hit:elite</c> is an error).
    /// </summary>
    private static Result<PlayerAction, string> SetRank(string token, PlayerAction action, EnemyTier rank) =>
        (action, rank) switch
        {
            (_, EnemyTier.Minor) => Ok(action),
            (PlayerAction.CastAbility { Hit: HitOutcome.Kill } cast, _) => Ok(cast with { Rank = rank }),
            (PlayerAction.FireWeapon { Hit: HitOutcome.Kill } fire, _) => Ok(fire with { Rank = rank }),
            (PlayerAction.PerformFinisher finisher, _) => Ok(finisher with { Rank = rank }),
            _ => FailUnknown(token),
        };

    /// <summary>The rank words of a kill (ADRs D45): <c>elite</c>, <c>miniboss</c>, <c>champion</c>, <c>boss</c>; a minor is said by none.</summary>
    private static bool IsRankWord(string word) =>
        word is "elite" or "miniboss" or "champion" or "boss";

    private static EnemyTier ToRank(string word) =>
        word switch
        {
            "elite" => EnemyTier.Elite,
            "miniboss" => EnemyTier.Miniboss,
            "champion" => EnemyTier.Champion,
            _ => EnemyTier.Boss,
        };

    /// <summary>What a token's options are for: none, an ability's charge (<c>:last</c>), a weapon's (<c>:shield</c>, <c>:precision</c>), or no step's.</summary>
    private enum OptionKind { None, Charge, Weapon, Invalid }

    /// <summary>The option words at the end of an ability's, weapon's or finisher's token (ADRs D36); none for any other token.</summary>
    private static string[] ReadOptions(string[] all) =>
        all.Length > 1 && all[0] is "grenade" or "melee" or "super" or "class" or "kinetic" or "energy" or "power" or "finisher"
            ? [.. all.Skip(1).Reverse().TakeWhile(word => word is "last" or "shield" or "precision" || IsRankWord(word)).Reverse()]
            : [];

    /// <summary><c>:last</c> stands alone; <c>:shield</c> and <c>:precision</c> go together, each once.</summary>
    private static OptionKind ClassifyOptions(string[] options) =>
        options switch
        {
            [] => OptionKind.None,
            ["last"] => OptionKind.Charge,
            _ when options.All(word => word is "shield" or "precision") && options.Distinct().Count() == options.Length => OptionKind.Weapon,
            _ => OptionKind.Invalid,
        };

    /// <summary>An ability's token: its secondary input first when it has one (<c>melee:slide</c>), then <c>[:hit|kill[:N]]</c>.</summary>
    private static Result<PlayerAction, string> ParseCast(string token, string[] parts, OffensiveAbility kind, ChargeUse charge) =>
        (parts.Length > 1 ? FindSecondaryInput(kind.ToAbilityKind(), parts[1]) : Optional.None<AbilityInput>()).Match(
            input => ParseAimedAction(token, [parts[0], .. parts[2..]], (hit, targets) => new PlayerAction.CastAbility(kind, hit, targets, input.Value, charge)),
            _ => ParseAimedAction(token, parts, (hit, targets) => new PlayerAction.CastAbility(kind, hit, targets, AbilityInput.Press, charge)));

    /// <summary>The secondary input a token part names (<c>slide</c>, <c>air</c>, <c>hold</c>, <c>combo</c>, <c>guard</c>), when the ability takes it.</summary>
    private static Optional<AbilityInput> FindSecondaryInput(AbilityKind kind, string word) =>
        kind.ListSecondaryInputs().Where(input => input.ToInputWord() == word).Select(Optional.Some).FindFirstSome();

    /// <summary>The <c>[:hit|kill[:N]]</c> suffix of an ability or weapon token.</summary>
    private static Result<PlayerAction, string> ParseAimedAction(
        string token, string[] parts, Func<HitOutcome, TargetCount, PlayerAction> create) =>
        (parts.Length, parts.Length > 1 ? parts[1] : "") switch
        {
            (1, _) => Ok(create(HitOutcome.Damage, TargetCount.One)),
            (2 or 3, "hit" or "kill") => ParseTargetCount(token, parts)
                .Map(targets => create(parts[1] == "kill" ? HitOutcome.Kill : HitOutcome.Damage, targets)),
            _ => FailUnknown(token),
        };

    private static Result<TargetCount, string> ParseTargetCount(string token, string[] parts) =>
        parts.Length == 2
            ? new Result<TargetCount, string>.Ok(TargetCount.One)
            : int.TryParse(parts[2], NumberStyles.None, Invariant, out var count) && TargetCount.TryFrom(count, out var targets)
                ? new Result<TargetCount, string>.Ok(targets)
                : new Result<TargetCount, string>.Error(
                    $"Invalid target count in '{token}': use a whole number from 1 to {TargetCount.Maximum} (enemies hit or killed).");

    private static Result<PlayerAction, string> Ok(PlayerAction action) => new Result<PlayerAction, string>.Ok(action);

    /// <summary>The slot a token names: <c>kinetic</c>, <c>energy</c>, <c>power</c>.</summary>
    private static Optional<WeaponSlot> FindWeaponSlot(string word) =>
        word switch
        {
            "kinetic" => Optional.Some(WeaponSlot.Kinetic),
            "energy" => Optional.Some(WeaponSlot.Energy),
            "power" => Optional.Some(WeaponSlot.Power),
            _ => Optional.None<WeaponSlot>(),
        };

    /// <summary>The brick a token names: <c>special</c>, <c>heavy</c>.</summary>
    private static Optional<AmmoBrick> FindAmmoBrick(string word) =>
        word switch
        {
            "special" => Optional.Some(AmmoBrick.Special),
            "heavy" => Optional.Some(AmmoBrick.Heavy),
            _ => Optional.None<AmmoBrick>(),
        };

    private static Result<PlayerAction, string> FailUnknown(string token) =>
        new Result<PlayerAction, string>.Error($"Unknown action '{token}'. Use {Grammar} (N = enemies hit or killed, 1..{TargetCount.Maximum}).");
}
