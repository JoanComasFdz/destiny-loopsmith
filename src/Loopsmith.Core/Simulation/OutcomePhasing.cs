using Loopsmith.Core.Domain;

namespace Loopsmith.Core.Simulation;

/// <summary>Order inside one event: debuffs land before damage bonuses are counted, refunds come last.</summary>
public enum Phase { Debuff, Empower, Damage, Spawn, Refund }

public static class OutcomePhasing
{
    public static Phase ResolvePhase(this Outcome outcome) =>
        outcome.Match(
            grantEnergy: _ => Phase.Refund,
            convertStacksToEnergy: _ => Phase.Refund,
            applyBuff: _ => Phase.Empower,
            removeBuff: _ => Phase.Empower,
            debuffTarget: _ => Phase.Debuff,
            spawn: _ => Phase.Spawn,
            spawnSummon: _ => Phase.Damage,
            strikeTarget: _ => Phase.Damage,
            modifyDamage: _ => Phase.Empower,
            restoreHealth: _ => Phase.Refund,
            resetCooldown: _ => Phase.Refund);
}
