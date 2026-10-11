using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.Phrasing;
using Loopsmith.Core.TraceRendering;

namespace Loopsmith.Web.Presentation;

/// <summary>A weapon a weapon buff is on: its name and icon (Slice: Festival Flight).</summary>
public sealed record BuffWeapon(string Name, Optional<string> Icon);

/// <summary>Pure: what a step's state draws next to a weapon buff — the build's weapons whose own perks give it (Core says which).</summary>
public static class StateHudShaping
{
    public static ImmutableArray<BuffWeapon> ListBuffWeapons(ValidatedBuild build, StateEntry entry) =>
        entry.Keyword is KeywordRef.Status status
            ?
            [
                .. LoopDesigning.ListWeaponsGiving(build, status.Id)
                    .Select(index => build.Build.Weapons[index])
                    .Select(weapon => new BuffWeapon(
                        weapon.Name,
                        weapon.Hash.Bind(hash => build.Catalog.Manifest.FindItem(hash)).Bind(item => item.Icon).Map(LoadoutShaping.ToIconUrl))),
            ]
            : [];
}
