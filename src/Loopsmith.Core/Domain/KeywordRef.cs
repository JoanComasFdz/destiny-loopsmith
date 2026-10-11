using Dunet;

namespace Loopsmith.Core.Domain;

/// <summary>Which keyword something names: a status, a pickup or a summon, by its id (a phrase's keyword, a state's entry).</summary>
[Union]
public partial record KeywordRef
{
    partial record Status(StatusId Id);
    partial record Pickup(PickupId Id);
    partial record Summon(SummonId Id);
}
