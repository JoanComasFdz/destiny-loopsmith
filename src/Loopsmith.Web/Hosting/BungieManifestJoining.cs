using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.ManifestJoining;
using Loopsmith.Core.Orchestration;
using Loopsmith.Web.Presentation;
using Microsoft.JSInterop;

namespace Loopsmith.Web.Hosting;

/// <summary>
/// Impure shell: Bungie's full manifest, loaded once when the app starts and kept for the tab, as DIM does
/// (<see cref="LoadAtStartupAsync"/>: its components, then the listing of every weapon and armor piece of the game, the
/// pickers' "Every item"; the startup screen shows <see cref="Startup"/> until it is done). The browser keeps a copy of
/// both between visits (ADRs D25): the load asks the manifest index for the current version first, and when the saved
/// copy is that version (or the index doesn't answer) it reads the components from there instead of downloading them,
/// and the listing too when the same Loopsmith code made it (<c>codeVersion</c>); else it downloads them (a file whose
/// download breaks starts again from its start, up to <see cref="Retries"/> more times) and lists them, and once the
/// load is done saves them in place of the old copy. Then it answers the hashes the bundled excerpt lacks (a pasted DIM
/// link's other items, a login's armor and weapons, an item picked from "Every item") from the components the tab holds:
/// Core asks for their entries round by round (each round asks only what it hasn't asked, so the rounds end) and builds
/// the excerpt. When the player went on without them (the startup load failed), the first read that needs them loads
/// them, and they stay. One read runs at a time: another (the page answers taps meanwhile, so a login can come during a
/// DIM link's read) waits for it. The page keeps answering taps: reading the files pauses every few megabytes, and the
/// join and the listing give the browser a turn before each round. <see cref="Reading"/> says how far a read got (the
/// Bungie login section and the ⋯ pickers show it as a progress bar), <see cref="ReadingChanged"/> when it moved. The
/// component URLs come from the manifest index (sent with <c>BungieApiKey</c> when set), else from the pinned content id
/// (<c>ManifestContentId</c> in wwwroot/appsettings.json). It remembers the hashes it was asked for in this tab, so the
/// designer doesn't read again what Home just read (or failed to read).
/// </summary>
public sealed class BungieManifestJoining(BrowserInterop browser, string apiKey, string pinnedContentId, string codeVersion)
{
    /// <summary>How many more times a component file whose download broke is downloaded again, from its start.</summary>
    public const int Retries = 3;

    private readonly SemaphoreSlim oneAtATime = new(1, 1);

    /// <summary>The URLs of the manifest's components the tab holds (they stay once in), when it holds them.</summary>
    private Optional<ImmutableDictionary<string, string>> held = Optional.None<ImmutableDictionary<string, string>>();

    /// <summary>The components downloaded and not saved yet (they're saved once the read that needed them is done).</summary>
    private Optional<ImmutableDictionary<string, string>> unsavedFiles = Optional.None<ImmutableDictionary<string, string>>();

    /// <summary>The listing made and not saved yet, with the key it is saved under.</summary>
    private Optional<KeyValuePair<string, ItemListing>> unsavedListing = Optional.None<KeyValuePair<string, ItemListing>>();

    /// <summary>The hashes the manifest was read for in this tab (found, missing or failed).</summary>
    public ImmutableHashSet<ItemHash> Asked { get; private set; } = [];

    /// <summary>Where the startup load is (the startup screen stands in for the page until it is done or skipped).</summary>
    public ManifestStartup Startup { get; private set; } = ManifestStartupShaping.NotBegun;

    /// <summary>The startup load moved on (or ended).</summary>
    public event Action? StartupChanged;

    /// <summary>The read in progress, when there is one.</summary>
    public Optional<ManifestReadProgress> Reading { get; private set; } = Optional.None<ManifestReadProgress>();

    /// <summary>The read in progress moved on (or ended).</summary>
    public event Action? ReadingChanged;

    /// <summary>Every weapon and armor piece of the game, once listed in this tab.</summary>
    public Optional<ItemListing> Listing { get; private set; } = Optional.None<ItemListing>();

    /// <summary>
    /// The startup load, once per tab (another call while it runs, or after it, does nothing): the manifest's components
    /// (the saved copy, or a download), then the listing of every weapon and armor piece (the saved one, or made), both
    /// kept for the tab; <see cref="Startup"/> says where it is. What it downloaded or made is saved once it is done.
    /// </summary>
    public async Task LoadAtStartupAsync()
    {
        if (Startup is not ManifestStartup.Waiting)
        {
            return;
        }

        ShowStartup(ManifestStartupShaping.CheckBegun);                                         // impure (tab state)
        await oneAtATime.WaitAsync();                                                            // impure
        try
        {
            var listed = await LoadAndListItemsAsync();                                          // impure
            KeepListing(listed);                                                                 // impure (tab state)
            var ended = ManifestStartupShaping.EndStartup(listed);                               // pure
            ShowStartup(ended);                                                                  // impure (tab state)
        }
        finally
        {
            oneAtATime.Release();                                                                // impure
        }

        _ = SaveAsync();                                                                         // impure (the browser's saved copy, meanwhile)
    }

    /// <summary>"Try again" after the startup load failed: it runs again from where the tab is (the files already in stay).</summary>
    public Task TryStartupAgainAsync()
    {
        if (Startup is not ManifestStartup.Failed)
        {
            return Task.CompletedTask;
        }

        ShowStartup(ManifestStartupShaping.NotBegun);                                           // impure (tab state)
        return LoadAtStartupAsync();                                                             // impure
    }

    /// <summary>"Continue without it" after the startup load failed: the page opens; the next read that needs the manifest loads it.</summary>
    public void SkipStartup()
    {
        if (Startup is ManifestStartup.Failed)
        {
            ShowStartup(ManifestStartupShaping.Skipped);                                         // impure (tab state)
        }
    }

    /// <summary>The excerpt of what <paramref name="hashes"/> (<paramref name="readFor"/>: whose they are) need, from the full manifest; or why it couldn't be read.</summary>
    public async Task<Result<ManifestExcerpt, string>> JoinAsync(ImmutableArray<ItemHash> hashes, ManifestReadFor readFor)
    {
        var asked = Asked.Union(hashes);                                                         // pure
        Asked = asked;                                                                           // impure (tab state)
        await oneAtATime.WaitAsync();                                                            // impure
        try
        {
            return await ReadAsync(hashes, readFor);                                             // impure
        }
        finally
        {
            ShowReading(Optional.None<ManifestReadProgress>());                                  // impure (tab state)
            oneAtATime.Release();                                                                // impure
            _ = SaveAsync();                                                                     // impure (the browser's saved copy, meanwhile)
        }
    }

    /// <summary>
    /// Every weapon and armor piece a build can wear (the pickers' "Every item"), each weapon with its breaker: the
    /// listing the startup load made, else read now (its weapons' frames and their perks too) and kept (a read that failed
    /// is tried again next time); or why it couldn't be read. One read at a time, as <see cref="JoinAsync"/>.
    /// </summary>
    public async Task<Result<ItemListing, string>> ListItemsAsync()
    {
        if (Listing is Optional<ItemListing>.Some listed)
        {
            return new Result<ItemListing, string>.Ok(listed.Value);
        }

        await oneAtATime.WaitAsync();                                                            // impure
        try
        {
            var read = Listing is Optional<ItemListing>.Some meanwhile
                ? new Result<ItemListing, string>.Ok(meanwhile.Value)
                : await ReadListingAsync();                                                      // impure
            KeepListing(read);                                                                   // impure (tab state)
            return read;
        }
        finally
        {
            ShowReading(Optional.None<ManifestReadProgress>());                                  // impure (tab state)
            oneAtATime.Release();                                                                // impure
            _ = SaveAsync();                                                                     // impure (the browser's saved copy, meanwhile)
        }
    }

    /// <summary>
    /// How many bytes of the manifest's components have come, unpacked (downloaded, or read from the saved copy), and which
    /// try the file coming is on (manifest.js, a few times a second).
    /// </summary>
    [JSInvokable]
    public void ReportDownload(double bytes, int attempt)
    {
        var download = ManifestProgressShaping.ToDownload((long)bytes, attempt, Retries);        // pure
        var startup = ManifestStartupShaping.ReportDownload(Startup, download);                  // pure
        var reading = Reading.Map(progress => ManifestProgressShaping.ReportDownload(progress, download)); // pure
        if (ManifestStartupShaping.IsAtFiles(Startup))
        {
            ShowStartup(startup);                                                                // impure (tab state)
        }

        if (Reading.IsSome())
        {
            ShowReading(reading);                                                                // impure (tab state)
        }
    }

    /// <summary>
    /// The manifest's components when the tab doesn't hold them yet (then kept): the browser's saved copy when it is the
    /// current version (and the listing saved with it, when <paramref name="withListing"/> and the same code made it),
    /// else a download; or why they couldn't be had.
    /// </summary>
    private async Task<Result<HeldFiles, string>> LoadFilesAsync(bool withListing)
    {
        if (held is Optional<ImmutableDictionary<string, string>>.Some holding)
        {
            return new Result<HeldFiles, string>.Ok(new HeldFiles(holding.Value, Optional.None<ItemListing>()));
        }

        var index = await browser.ReadManifestIndexAsync(LoopDesigning.ManifestIndexUrl, apiKey); // impure
        var savedJson = await browser.ReadSavedManifestAsync();                                  // impure
        var saved = LoopDesigning.ReadSavedManifest(savedJson);                                  // pure
        var source = LoopDesigning.ChooseManifestSource(index, pinnedContentId, saved);          // pure
        return await source.Match(
            fromSaved => ReadSavedFilesAsync(fromSaved, withListing),
            download => DownloadFilesAsync(download.Urls));                                      // impure
    }

    /// <summary>The components (and the listing, when asked and saved for this code) from the browser's saved copy; a download when it doesn't read.</summary>
    private async Task<Result<HeldFiles, string>> ReadSavedFilesAsync(ManifestSource.Saved fromSaved, bool withListing)
    {
        var urls = fromSaved.Copy.Urls;                                                          // pure
        var key = LoopDesigning.ToListingKey(urls, codeVersion);                                 // pure
        var savedText = Optional.None<string>();                                                 // pure
        if (withListing)
        {
            savedText = await browser.ReadSavedListingAsync(key);                                // impure
        }

        var listing = LoopDesigning.ReadSavedListing(savedText);                                 // pure
        var reading = ManifestStartupShaping.BeginReadingSaved(fromSaved.Copy.Bytes, listing.IsSome()); // pure
        if (Startup is ManifestStartup.Checking)
        {
            ShowStartup(reading);                                                                // impure (tab state)
        }

        using var reporter = DotNetObjectReference.Create(this);                                 // impure
        var files = await browser.LoadSavedManifestTablesAsync(urls, reporter);                  // impure
        if (files is Result<Unit, string>.Error)
        {
            return await DownloadFilesAsync(fromSaved.Fallback);                                 // impure
        }

        held = Optional.Some(urls);                                                              // impure (tab state)
        return new Result<HeldFiles, string>.Ok(new HeldFiles(urls, listing));
    }

    /// <summary>The components downloaded (to be saved once the read that needed them is done); or why they couldn't be.</summary>
    private async Task<Result<HeldFiles, string>> DownloadFilesAsync(ImmutableDictionary<string, string> urls)
    {
        if (Startup is ManifestStartup.Checking or ManifestStartup.ReadingSaved)
        {
            ShowStartup(ManifestStartupShaping.DownloadBegun);                                   // impure (tab state)
        }

        using var reporter = DotNetObjectReference.Create(this);                                 // impure
        var files = await browser.LoadManifestTablesAsync(urls, Retries, reporter);              // impure
        var downloaded = files.Map(_ => new HeldFiles(urls, Optional.None<ItemListing>()));      // pure
        if (downloaded is Result<HeldFiles, string>.Ok)
        {
            held = Optional.Some(urls);                                                          // impure (tab state)
            unsavedFiles = Optional.Some(urls);                                                  // impure (tab state)
        }

        return downloaded;
    }

    private async Task<Result<ManifestExcerpt, string>> ReadAsync(ImmutableArray<ItemHash> hashes, ManifestReadFor readFor)
    {
        var started = held.IsSome()
            ? ManifestProgressShaping.StartNaming(readFor, hashes.Length)
            : ManifestProgressShaping.StartReading(readFor, hashes.Length);                      // pure
        ShowReading(Optional.Some(started));                                                     // impure (tab state)
        var files = await LoadFilesAsync(withListing: false);                                    // impure
        var step = files.Map(_ => LoopDesigning.StartManifestJoin(hashes));                      // pure
        while (step is Result<ManifestJoinStep, string>.Ok { Value: ManifestJoinStep.Ask ask })
        {
            var request = LoopDesigning.ToManifestRequest(ask.Query);                            // pure
            var search = LoopDesigning.ToManifestPlugSearch(ask.Query);                          // pure
            var entries = await browser.ReadManifestEntriesAsync(request);                       // impure
            var found = await browser.FindManifestEntriesContainingAsync(search);                // impure
            var naming = Reading.Map(ManifestProgressShaping.ReportRound);                       // pure
            ShowReading(naming);                                                                 // impure (tab state)
            await Task.Delay(1);                                                                 // impure (the browser's turn: taps, the bar)
            step = LoopDesigning.ContinueManifestJoin(ask, entries, found);                      // pure
        }

        return LoopDesigning.ToJoinedExcerpt(step);                                              // pure
    }

    /// <summary>The listing read when the startup load didn't make it (skipped, or failed at it), with the read's progress bar.</summary>
    private async Task<Result<ItemListing, string>> ReadListingAsync()
    {
        var started = held.IsSome()
            ? ManifestProgressShaping.StartNaming(ManifestReadFor.EveryItem, 0)
            : ManifestProgressShaping.StartReading(ManifestReadFor.EveryItem, 0);                // pure
        ShowReading(Optional.Some(started));                                                     // impure (tab state)
        return await LoadAndListItemsAsync();                                                    // impure
    }

    /// <summary>
    /// The manifest's components (when the tab doesn't hold them yet), then every weapon and armor piece: the listing saved
    /// with them, else made from them (to be saved once the read is done); or why not.
    /// </summary>
    private async Task<Result<ItemListing, string>> LoadAndListItemsAsync()
    {
        var files = await LoadFilesAsync(withListing: true);                                     // impure
        if (files is not Result<HeldFiles, string>.Ok { Value: var loaded })
        {
            return files.Map(_ => new ItemListing([], ImmutableDictionary<ItemHash, ArmorSet>.Empty)); // pure (its failure)
        }

        if (loaded.Listing is Optional<ItemListing>.Some saved)
        {
            return new Result<ItemListing, string>.Ok(saved.Value);
        }

        var listed = await ListAllItemsAsync();                                                  // impure
        var key = LoopDesigning.ToListingKey(loaded.Urls, codeVersion);                          // pure
        var unsaved = listed.Match(
            ok => Optional.Some(KeyValuePair.Create(key, ok.Value)),
            _ => Optional.None<KeyValuePair<string, ItemListing>>());                            // pure
        unsavedListing = unsaved;                                                                // impure (tab state)
        return listed;
    }

    /// <summary>
    /// Every weapon and armor piece, from the components the tab holds: the listing's questions, each answered by the page
    /// (the startup screen or the read's bar says which one, before the browser gets its turn), until it is done.
    /// </summary>
    private async Task<Result<ItemListing, string>> ListAllItemsAsync()
    {
        Result<ListingStep, string> step = new Result<ListingStep, string>.Ok(LoopDesigning.StartListing()); // pure
        while (step is Result<ListingStep, string>.Ok { Value: ListingStep.Ask ask })
        {
            var round = LoopDesigning.ToListingRound(ask);                                       // pure
            ReportListingRound(round);                                                           // impure (tab state)
            await browser.WaitForPaintAsync();                                                   // impure (the browser paints the bars, answers taps)
            var entries = await browser.ReadManifestEntriesAsync(ask.Query.Entries);            // impure
            var parts = await browser.ReadManifestPartsAsync(ask.Query.Parts);                  // impure
            step = LoopDesigning.ContinueListing(ask, entries, parts);                           // pure
        }

        return LoopDesigning.ToItemListing(step);                                                // pure
    }

    /// <summary>The listing's question the startup load (its screen) or the read in progress (its bar) is on.</summary>
    private void ReportListingRound(int round)
    {
        var listing = ManifestStartupShaping.ReportListing(Startup, round, LoopDesigning.ListingRounds); // pure
        var naming = Reading.Map(progress => ManifestProgressShaping.ReportRound(progress, round)); // pure
        if (ManifestStartupShaping.IsLoading(Startup))
        {
            ShowStartup(listing);                                                                // impure (tab state)
        }

        if (Reading.IsSome())
        {
            ShowReading(naming);                                                                 // impure (tab state)
        }
    }

    /// <summary>A listing read is kept for the tab; a failed one leaves none (the next opening tries again).</summary>
    private void KeepListing(Result<ItemListing, string> read)
    {
        var listing = read.Match(ok => Optional.Some(ok.Value), _ => Optional.None<ItemListing>()); // pure
        Listing = listing;                                                                       // impure (tab state)
    }

    /// <summary>
    /// Saves in the browser what this tab downloaded or made and hasn't saved yet: the components in place of the copy
    /// saved before, the listing in place of its own. A browser that can't save keeps working as it did: the next visit
    /// downloads again.
    /// </summary>
    private async Task SaveAsync()
    {
        var files = unsavedFiles;                                                                // pure
        var listing = unsavedListing;                                                            // pure
        unsavedFiles = Optional.None<ImmutableDictionary<string, string>>();                     // impure (tab state)
        unsavedListing = Optional.None<KeyValuePair<string, ItemListing>>();                     // impure (tab state)
        if (files is Optional<ImmutableDictionary<string, string>>.Some downloaded)
        {
            _ = await browser.SaveManifestTablesAsync(downloaded.Value);                         // impure (not saved: the next visit downloads again)
        }

        if (listing is Optional<KeyValuePair<string, ItemListing>>.Some made)
        {
            var text = LoopDesigning.ToSavedListing(made.Value.Value);                           // pure
            _ = await browser.SaveListingAsync(made.Value.Key, text);                            // impure (not saved: the next visit lists again)
        }
    }

    private void ShowStartup(ManifestStartup startup)
    {
        Startup = startup;                                                                       // impure (tab state)
        StartupChanged?.Invoke();                                                                // impure
    }

    private void ShowReading(Optional<ManifestReadProgress> reading)
    {
        Reading = reading;                                                                       // impure (tab state)
        ReadingChanged?.Invoke();                                                                // impure
    }
}

/// <summary>The components the tab holds (their URLs: their version), and the listing saved with them when it was read too.</summary>
internal sealed record HeldFiles(ImmutableDictionary<string, string> Urls, Optional<ItemListing> Listing);
