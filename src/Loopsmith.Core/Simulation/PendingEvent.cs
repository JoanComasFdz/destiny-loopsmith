using Dunet;
using Loopsmith.Core.Domain;

namespace Loopsmith.Core.Simulation;

/// <summary>
/// An event waiting to be emitted. Hits and kills snapshot the target only when emitted, so a kill
/// sees the debuffs its own hit applied (Spark of Shock jolts → the kill is a "jolted kill").
/// </summary>
[Union]
public partial record PendingEvent
{
    partial record Ready(GameEvent Event);
    partial record HitTarget(DamageOrigin Origin);
    partial record KillTarget(DamageOrigin Origin);
}
