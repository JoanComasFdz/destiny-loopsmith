using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.ManifestJoining;
using Loopsmith.Core.Orchestration;
using Loopsmith.Web.Presentation;
using Microsoft.JSInterop;

namespace Loopsmith.Web.Hosting;

/// <summary>
/// Impure shell: Bungie's full manifest for the hashes the bundled excerpt lacks (a pasted DIM link's other items, a
/// login's armor and weapons, an item picked from "Every item"), and the listing of every weapon and armor piece in the
/// game (<see cref="ListItemsAsync"/>: from the manifest's lite item table, once per tab). The components download from bungie.net (the browser's HTTP cache keeps them; the app
/// stores nothing, ADRs D25) and are indexed as they come; Core asks for their entries round by round (each round asks
/// only what it hasn't asked, so the rounds end) and builds the excerpt; then, when no other read waits for them, the
/// components are let go (they are most of the tab's memory). One read runs at a time: another (the page answers taps
/// meanwhile, so a login can come during a DIM link's read) waits for it. The page keeps answering taps: the download
/// pauses every few megabytes, and the join gives the browser a turn before each round. <see cref="Reading"/> says how
/// far the read got (the Bungie login section and the ⋯ pickers show it as a progress bar), <see cref="ReadingChanged"/>
/// when it moved. The component URLs come from the manifest index (sent with <c>BungieApiKey</c> when set), else from
/// the pinned content id (<c>ManifestContentId</c> in wwwroot/appsettings.json). It remembers the hashes it was asked
/// for in this tab, so the designer doesn't read again what Home just read (or failed to read).
/// </summary>
public sealed class BungieManifestJoining(BrowserInterop browser, string apiKey, string pinnedContentId)
{
    private readonly SemaphoreSlim oneAtATime = new(1, 1);
    private int waiting;

    /// <summary>The hashes the manifest was read for in this tab (found, missing or failed).</summary>
    public ImmutableHashSet<ItemHash> Asked { get; private set; } = [];

    /// <summary>The read in progress, when there is one.</summary>
    public Optional<ManifestReadProgress> Reading { get; private set; } = Optional.None<ManifestReadProgress>();

    /// <summary>The read in progress moved on (or ended).</summary>
    public event Action? ReadingChanged;

    /// <summary>The excerpt of what <paramref name="hashes"/> (<paramref name="readFor"/>: whose they are) need, from the full manifest; or why it couldn't be read.</summary>
    public async Task<Result<ManifestExcerpt, string>> JoinAsync(ImmutableArray<ItemHash> hashes, ManifestReadFor readFor)
    {
        var asked = Asked.Union(hashes);                                                         // pure
        Asked = asked;                                                                           // impure (tab state)
        waiting++;                                                                               // impure (tab state)
        await oneAtATime.WaitAsync();                                                            // impure
        waiting--;                                                                               // impure (tab state)
        try
        {
            return await ReadAsync(hashes, readFor);                                             // impure
        }
        finally
        {
            await ReleaseAsync();                                                                // impure
            oneAtATime.Release();                                                                // impure
        }
    }

    /// <summary>Every weapon and armor piece of the game, once listed in this tab.</summary>
    public Optional<ItemListing> Listing { get; private set; } = Optional.None<ItemListing>();

    /// <summary>
    /// Every weapon and armor piece a build can wear (the pickers' "Every item"): read once per tab from the manifest's
    /// lite item table, its buckets and its sets, then kept (a read that failed is tried again next time); or why it
    /// couldn't be read. One read at a time, as <see cref="JoinAsync"/>.
    /// </summary>
    public async Task<Result<ItemListing, string>> ListItemsAsync()
    {
        if (Listing is Optional<ItemListing>.Some listed)
        {
            return new Result<ItemListing, string>.Ok(listed.Value);
        }

        waiting++;                                                                               // impure (tab state)
        await oneAtATime.WaitAsync();                                                            // impure
        waiting--;                                                                               // impure (tab state)
        try
        {
            var read = Listing is Optional<ItemListing>.Some meanwhile
                ? new Result<ItemListing, string>.Ok(meanwhile.Value)
                : await ReadListingAsync();                                                      // impure
            Listing = read.Match(ok => Optional.Some(ok.Value), _ => Optional.None<ItemListing>()); // impure (tab state)
            return read;
        }
        finally
        {
            await ReleaseAsync();                                                                // impure
            oneAtATime.Release();                                                                // impure
        }
    }

    /// <summary>How many bytes of the manifest's components have come, unpacked (manifest.js, a few times a second).</summary>
    [JSInvokable]
    public void ReportDownload(double bytes)
    {
        var moved = Reading.Map(progress => ManifestProgressShaping.ReportBytes(progress, (long)bytes)); // pure
        ShowReading(moved);                                                                      // impure (tab state)
    }

    private async Task<Result<ManifestExcerpt, string>> ReadAsync(ImmutableArray<ItemHash> hashes, ManifestReadFor readFor)
    {
        var started = ManifestProgressShaping.StartReading(readFor, hashes.Length);              // pure
        ShowReading(Optional.Some(started));                                                     // impure (tab state)
        using var reporter = DotNetObjectReference.Create(this);                                 // impure
        var index = await browser.ReadManifestIndexAsync(LoopDesigning.ManifestIndexUrl, apiKey); // impure
        var urls = LoopDesigning.ChooseManifestUrls(index, pinnedContentId);                     // pure
        var loaded = await browser.LoadManifestTablesAsync(urls, reporter);                      // impure
        var step = loaded.Map(_ => LoopDesigning.StartManifestJoin(hashes));                     // pure
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

    private async Task<Result<ItemListing, string>> ReadListingAsync()
    {
        var started = ManifestProgressShaping.StartReading(ManifestReadFor.EveryItem, 0);       // pure
        ShowReading(Optional.Some(started));                                                     // impure (tab state)
        using var reporter = DotNetObjectReference.Create(this);                                 // impure
        var index = await browser.ReadManifestIndexAsync(LoopDesigning.ManifestIndexUrl, apiKey); // impure
        var urls = LoopDesigning.ChooseListingUrls(index, pinnedContentId);                      // pure
        var loaded = await browser.LoadManifestTablesAsync(urls, reporter);                      // impure
        return await loaded.BindAsync(_ => ReadListedTablesAsync());                             // impure
    }

    /// <summary>The listing's small components (the buckets, the sets), then its items.</summary>
    private async Task<Result<ItemListing, string>> ReadListedTablesAsync()
    {
        var request = LoopDesigning.ToListingTableRequest();                                     // pure
        var tablesJson = await browser.ReadManifestEntriesAsync(request);                       // impure
        var tables = LoopDesigning.ReadListingTables(tablesJson);                                // pure
        return await tables.BindAsync(ReadListedItemsAsync);                                     // impure
    }

    /// <summary>The lite items of the weapon and armor buckets, cut down to what the pickers need, as the listing.</summary>
    private async Task<Result<ItemListing, string>> ReadListedItemsAsync(ListingTables tables)
    {
        var naming = Reading.Map(ManifestProgressShaping.ReportRound);                           // pure
        ShowReading(naming);                                                                     // impure (tab state)
        await Task.Delay(1);                                                                     // impure (the browser's turn: the bar)
        var query = LoopDesigning.ToListingPartsQuery(tables);                                   // pure
        var partsJson = await browser.ReadManifestPartsAsync(query);                             // impure
        return LoopDesigning.ReadItemListing(tables, partsJson);                                 // pure
    }

    /// <summary>The read is over (done or failed): the bar goes, and the components, unless another read waits for them.</summary>
    private async Task ReleaseAsync()
    {
        ShowReading(Optional.None<ManifestReadProgress>());                                      // impure (tab state)
        if (waiting == 0)
        {
            await browser.ReleaseManifestTablesAsync();                                          // impure
        }
    }

    private void ShowReading(Optional<ManifestReadProgress> reading)
    {
        Reading = reading;                                                                       // impure (tab state)
        ReadingChanged?.Invoke();                                                                // impure
    }
}
