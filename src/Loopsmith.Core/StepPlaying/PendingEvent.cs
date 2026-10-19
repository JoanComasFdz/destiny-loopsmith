using Dunet;
using Loopsmith.Core.Domain;

namespace Loopsmith.Core.StepPlaying;

/// <summary>
/// An event waiting to be emitted. Hits and kills snapshot the target only when emitted, so a kill
/// sees the debuffs its own hit applied (Spark of Shock jolts → the kill is a "jolted kill"). A hit carries how much damage
/// it stands for: a hit step's is sustained, a killing blow's, a strike's or a summon's one blow (ADRs D34).
/// </summary>
[Union]
public partial record PendingEvent
{
    partial record Ready(GameEvent Event);
    partial record HitTarget(DamageOrigin Origin, DamageSpan Span);
    partial record KillTarget(DamageOrigin Origin, HitZone Zone = HitZone.Body);   // Zone: a precision kill the player says (ADRs D36)
}
