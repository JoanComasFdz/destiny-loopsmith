using System.Collections.Immutable;
using Dunet;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Core.Orchestration;

/// <summary>Side effects described as data; the host executes them (console I/O lives in the host).</summary>
[Union]
public partial record Effect
{
    partial record WriteLines(ImmutableArray<StyledLine> Lines);
    partial record WriteText(string Text);
    partial record ShowFailure(string Message);
}
