using Dunet;

namespace Loopsmith.Core.Domain;

/// <summary>
/// A shape the game draws for a keyword that Bungie's data has no icon file for (rules/glossary.yaml, a status's
/// <c>drawnIcon</c>); the designer draws it too.
/// </summary>
public enum DrawnIcon
{
    Rounds,     // a weapon's rounds buff in your buffs: a triangle around a round (Unraveling Rounds)
}

/// <summary>
/// The icon shown with a name: a manifest icon (a bungie.net path), or a shape the game draws that Bungie's data has no
/// file for, in the keyword's affinity (<see cref="DrawnIcon"/>).
/// </summary>
[Union]
public partial record IconRef
{
    partial record Manifest(string Path);
    partial record Drawn(DrawnIcon Shape, Affinity Affinity);
}
