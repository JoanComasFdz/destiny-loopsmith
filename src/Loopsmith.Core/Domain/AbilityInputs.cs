using System.Collections.Immutable;

namespace Loopsmith.Core.Domain;

/// <summary>
/// A secondary use of an ability the build offers (Tempest Strike's sliding melee, Ascension's air move) and the equipped
/// elements whose rules name it — what makes it a different trigger from the ability's press.
/// </summary>
public sealed record SecondaryUse(AbilityKind Kind, AbilityInput Input, ImmutableArray<ElementId> NamedBy);

/// <summary>
/// Pure: which secondary input an ability can take — the Compendium's: a melee while sliding (Tempest Strike, Lightning
/// Surge, Consecration, Howl of the Storm) or airborne (Ballistic Slam), the class ability's air move (Ascension: the air
/// move spends its charge), a grenade held (Chaos Accelerant, Bleak Watcher, Mindspun Invocation, Unbreakable).
/// </summary>
public static class AbilityInputs
{
    private static readonly ImmutableDictionary<AbilityKind, ImmutableArray<AbilityInput>> Secondaries = new Dictionary<AbilityKind, ImmutableArray<AbilityInput>>
    {
        [AbilityKind.Grenade] = [AbilityInput.Hold],
        [AbilityKind.Melee] = [AbilityInput.Slide, AbilityInput.Air],
        [AbilityKind.ClassAbility] = [AbilityInput.Air],
        [AbilityKind.Super] = [],
    }.ToImmutableDictionary();

    /// <summary>Whether <paramref name="kind"/> can be used with <paramref name="input"/>: every ability with a press; a secondary only where the game has one.</summary>
    public static bool CanTake(this AbilityKind kind, AbilityInput input) =>
        input == AbilityInput.Press || ListSecondaryInputs(kind).Contains(input);

    /// <summary>The secondary inputs an ability can take (none for a Super).</summary>
    public static ImmutableArray<AbilityInput> ListSecondaryInputs(this AbilityKind kind) =>
        Secondaries.TryGetValue(kind, out var inputs) ? inputs : [];
}
