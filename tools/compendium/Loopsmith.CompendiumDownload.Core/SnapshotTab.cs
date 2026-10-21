using Loopsmith.Functional;

namespace Loopsmith.CompendiumDownload.Core;

/// <summary>
/// What a snapshot holds of one tab, as INDEX.md, README.md and the viewer read it: its place and name, its gid, its
/// <c>.md</c>'s size in bytes and its <c>.csv</c>'s text (each none when the file is missing), and Google's page for it
/// (<c>raw/&lt;stem&gt;.html</c>, none when missing).
/// </summary>
public sealed record SnapshotTab(int Place, SheetTab Tab, Optional<long> MarkdownBytes, Optional<string> Csv, Optional<string> RawHtml);
