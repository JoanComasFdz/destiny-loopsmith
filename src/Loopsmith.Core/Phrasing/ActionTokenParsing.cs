using System.Globalization;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Phrasing;

/// <summary>
/// Kernel: the textual form of a player action, read back (inverse of <c>DomainPhrasing.ToActionToken</c>).
/// Shared by the CLI, the loop-file parser and the web host, so all accept exactly the same tokens.
/// </summary>
public static class ActionTokenParsing
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public const string Grammar =
        "grenade|melee|super[:hit|kill[:N]], class[:air], kinetic|energy|power[:hit|kill[:N]], pickup:<id>, max:<status>, end:<status>";

    /// <summary>
    /// <c>grenade</c>, <c>grenade:kill</c>, <c>grenade:kill:3</c>, <c>kinetic:hit:5</c>, <c>class</c>, <c>class:air</c> (an air move
    /// that spends the class ability, like Ascension),
    /// <c>pickup:orb-of-power</c>, and the states the player declares (ADRs D3): <c>max:bolt-charge</c>, <c>end:amplified</c>.
    /// Without <c>:kill</c> the hit only damages; without a count it hits one enemy. The count is validated here, at the
    /// boundary (<see cref="TargetCount"/>: 1..20); whether a declaration holds is the engine's (a blocked step).
    /// </summary>
    public static Result<PlayerAction, string> ParseActionToken(string token)
    {
        var parts = token.Trim().ToLowerInvariant().Split(':');
        return parts[0] switch
        {
            "grenade" => ParseAimedAction(token, parts, (hit, targets) => new PlayerAction.CastAbility(OffensiveAbility.Grenade, hit, targets)),
            "melee" => ParseAimedAction(token, parts, (hit, targets) => new PlayerAction.CastAbility(OffensiveAbility.Melee, hit, targets)),
            "super" => ParseAimedAction(token, parts, (hit, targets) => new PlayerAction.CastAbility(OffensiveAbility.Super, hit, targets)),
            "class" when parts.Length == 1 => Ok(new PlayerAction.UseClassAbility()),
            "class" when parts.Length == 2 && parts[1] == "air" => Ok(new PlayerAction.UseClassAbility(Airborne: true)),
            "kinetic" => ParseAimedAction(token, parts, (hit, targets) => new PlayerAction.FireWeapon(WeaponSlot.Kinetic, hit, targets)),
            "energy" => ParseAimedAction(token, parts, (hit, targets) => new PlayerAction.FireWeapon(WeaponSlot.Energy, hit, targets)),
            "power" => ParseAimedAction(token, parts, (hit, targets) => new PlayerAction.FireWeapon(WeaponSlot.Power, hit, targets)),
            "pickup" when parts.Length == 2 && PickupId.TryFrom(parts[1], out var pickup) => Ok(new PlayerAction.CollectPickups(pickup)),
            "max" when parts.Length == 2 && StatusId.TryFrom(parts[1], out var maxed) =>
                Ok(new PlayerAction.Declare(new StateDeclaration.ReachMax(maxed))),
            "end" when parts.Length == 2 && StatusId.TryFrom(parts[1], out var ended) =>
                Ok(new PlayerAction.Declare(new StateDeclaration.EndStatus(ended))),
            _ => FailUnknown(token),
        };
    }

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

    private static Result<PlayerAction, string> FailUnknown(string token) =>
        new Result<PlayerAction, string>.Error($"Unknown action '{token}'. Use {Grammar} (N = enemies hit or killed, 1..{TargetCount.Maximum}).");
}
