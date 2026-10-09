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

    public static Result<PlayerAction, string> ParseActionToken(string token)
    {
        var parts = token.Trim().ToLowerInvariant().Split(':');
        var hit = parts.Length > 1 && parts[1] == "kill" ? HitOutcome.Kill : HitOutcome.Damage;
        var hitSuffixIsValid = parts.Length == 1 || (parts.Length == 2 && parts[1] is "kill" or "hit");
        return parts[0] switch
        {
            "grenade" when hitSuffixIsValid => Ok(new PlayerAction.CastAbility(OffensiveAbility.Grenade, hit)),
            "melee" when hitSuffixIsValid => Ok(new PlayerAction.CastAbility(OffensiveAbility.Melee, hit)),
            "super" when hitSuffixIsValid => Ok(new PlayerAction.CastAbility(OffensiveAbility.Super, hit)),
            "class" when parts.Length == 1 => Ok(new PlayerAction.UseClassAbility()),
            "kinetic" when hitSuffixIsValid => Ok(new PlayerAction.FireWeapon(WeaponSlot.Kinetic, hit)),
            "energy" when hitSuffixIsValid => Ok(new PlayerAction.FireWeapon(WeaponSlot.Energy, hit)),
            "power" when hitSuffixIsValid => Ok(new PlayerAction.FireWeapon(WeaponSlot.Power, hit)),
            "pickup" when parts.Length == 2 && PickupId.TryFrom(parts[1], out var pickup) => Ok(new PlayerAction.CollectPickups(pickup)),
            "wait" when parts.Length == 1 => Ok(new PlayerAction.Wait(Seconds.From(5m))),
            "wait" when parts.Length == 2
                && decimal.TryParse(parts[1].TrimEnd('s'), NumberStyles.Number, Invariant, out var seconds)
                && seconds > 0m => Ok(new PlayerAction.Wait(Seconds.From(seconds))),
            _ => new Result<PlayerAction, string>.Error(
                $"Unknown action '{token}'. Use grenade[:kill], melee[:kill], super[:kill], class, kinetic|energy|power[:kill], pickup:<id>, wait[:<seconds>]."),
        };

        static Result<PlayerAction, string> Ok(PlayerAction action) => new Result<PlayerAction, string>.Ok(action);
    }
}
