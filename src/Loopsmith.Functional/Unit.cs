using Dunet;

namespace Loopsmith.Functional;

/// <summary>The single "nothing interesting" value: <c>new Unit.Value()</c>.</summary>
[Union]
public partial record Unit
{
    partial record Value();
}
