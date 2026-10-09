using System.Collections.Immutable;
using Loopsmith.Core.Functional;
using Loopsmith.Web.Hosting;

namespace Loopsmith.Web.State;

/// <summary>Impure shells around the library: read it from localStorage once, write it back after an edit.</summary>
public static class LibraryStoring
{
    public static async Task LoadLibraryAsync(Workbench workbench, BrowserInterop browser)
    {
        if (workbench.IsLibraryLoaded)
        {
            return;
        }

        var json = await browser.ReadStorageAsync(LoopLibrary.StorageKey);                     // impure

        var parsed = LoopLibrary.ParseLibrary(json);                                            // pure
        workbench.Library = parsed.Match(ok => ok.Value, _ => []);                              // pure
        workbench.LibraryProblem = parsed.Match(_ => Optional.None<string>(), error => Optional.Some(error.Failure)); // pure
        workbench.IsLibraryLoaded = true;
    }

    /// <summary>Stores <paramref name="library"/> and makes it the workbench's; false when the browser refused to store it.</summary>
    public static async Task<bool> StoreLibraryAsync(Workbench workbench, BrowserInterop browser, ImmutableArray<SavedLoop> library)
    {
        var json = LoopLibrary.WriteLibrary(library);                                           // pure
        var stored = await browser.WriteStorageAsync(LoopLibrary.StorageKey, json);             // impure
        workbench.Library = library;
        workbench.LibraryProblem = Optional.None<string>();
        return stored;
    }
}
