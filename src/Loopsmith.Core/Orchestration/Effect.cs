using System.Collections.Immutable;
using Dunet;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Core.Orchestration;

/// <summary>Side effects described as data; the host executes them (console and file I/O live in the host).</summary>
[Union]
public partial record Effect
{
    partial record WriteLines(ImmutableArray<StyledLine> Lines);
    partial record WriteText(string Text);
    partial record ShowFailure(string Message);

    /// <summary>Write <see cref="Text"/> to <see cref="Path"/> (creating its folder); show <see cref="Saved"/> once written, else the error.</summary>
    partial record SaveFile(string Path, string Text, ImmutableArray<StyledLine> Saved);
}
