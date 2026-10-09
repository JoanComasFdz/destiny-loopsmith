using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Web.Hosting;

namespace Loopsmith.Web.State;

/// <summary>Pure: the rows of the library (saved loops first, then the bundled examples) and opening one.</summary>
public static class LibraryListing
{
    public const string CurrentDesignId = "current";
    private const string BundledPrefix = "bundled:";

    public static ImmutableArray<LibraryEntry> ListEntries(LoopsmithBundle bundle, ImmutableArray<SavedLoop> saved) =>
    [
        .. saved.Select(loop => new LibraryEntry(
            loop.Id, loop.Name, loop.BuildName, loop.Steps, LibraryOrigin.Saved, Optional.Some(loop.SavedAt), loop.LoopFile, Optional.None<string>())),
        .. bundle.Loops.Select(DescribeBundledLoop),
    ];

    /// <summary>Replays a library entry's loop file into a design session.</summary>
    public static Result<DesignSession, string> OpenEntry(RuleCatalog catalog, LibraryEntry entry) =>
        LoopDesigning.ImportLoop(catalog, new SourceText(LoopLibrary.ToLoopFileName(entry.Name), entry.LoopFile));

    public static Optional<LibraryEntry> FindEntry(ImmutableArray<LibraryEntry> entries, string id) =>
        Optional.FromNullable(entries.FirstOrDefault(entry => entry.Id == id));

    /// <summary>A saved entry for the current design (its loop file comes from <see cref="LoopDesigning.ExportLoop"/>).</summary>
    public static SavedLoop ToSavedLoop(DesignSession session, string id, DateTimeOffset savedAt) =>
        new(id, session.Design.Name, session.Build.Build.Name, session.Design.Steps.Length, savedAt, LoopDesigning.ExportLoop(session));

    /// <summary>
    /// A saved loop under a new name. The loop file is rewritten through the core (import → rename → export) so the
    /// file's own <c>loop:</c> name matches; if the file does not import, only the library label changes.
    /// </summary>
    public static SavedLoop RenameSavedLoop(RuleCatalog catalog, SavedLoop loop, string name)
    {
        var imported = LoopDesigning.ImportLoop(catalog, new SourceText(LoopLibrary.ToLoopFileName(loop.Name), loop.LoopFile));
        var file = imported.Match(
            ok => LoopDesigning.ExportLoop(LoopDesigning.RenameDesign(ok.Value, name, ok.Value.Design.Author, ok.Value.Design.Description)),
            _ => loop.LoopFile);
        return loop with { Name = name, LoopFile = file };
    }

    private static LibraryEntry DescribeBundledLoop(BundledLoop loop)
    {
        var fileName = loop.File.Path.Split('/')[^1];
        return loop.Opened.Match(
            ok => new LibraryEntry(
                BundledPrefix + loop.File.Path, ok.Value.Design.Name, ok.Value.Build.Build.Name, ok.Value.Design.Steps.Length,
                LibraryOrigin.Bundled, Optional.None<DateTimeOffset>(), loop.File.Text, Optional.None<string>()),
            error => new LibraryEntry(
                BundledPrefix + loop.File.Path, fileName.Replace(".loop.yaml", "", StringComparison.Ordinal), loop.Slug, 0,
                LibraryOrigin.Bundled, Optional.None<DateTimeOffset>(), loop.File.Text, Optional.Some(error.Failure)));
    }
}
