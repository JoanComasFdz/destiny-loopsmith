namespace Loopsmith.Core.Domain;

/// <summary>A text file as read by SourceFetching and handed to a parsing slice (path relative to its root).</summary>
public sealed record SourceText(string Path, string Text);
