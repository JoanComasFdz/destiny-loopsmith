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
/// pickers' "Every item"; the startup screen shows <see cref="Startup"/> until it is done). Then it answers the hashes the
/// bundled excerpt lacks (a pasted DIM link's other items, a login's armor and weapons, an item picked from "Every item")
/// from the components the tab holds: Core asks for their entries round by round (each round asks only what it hasn't
/// asked, so the rounds end) and builds the excerpt. The components download from bungie.net (the browser's HTTP cache
/// keeps them; the app stores nothing, ADRs D25) and are indexed as they come; a file whose download breaks starts again
/// from its start, up to <see cref="Retries"/> more times. When the player went on without them (the startup load
/// failed), the first read that needs them loads them, and they stay. One read runs at a time: another (the page answers
/// taps meanwhile, so a login can come during a DIM link's read) waits for it. The page keeps answering taps: the
/// download pauses every few megabytes, and the join and the listing give the browser a turn before each round.
/// <see cref="Reading"/> says how far a read got (the Bungie login section and the ⋯ pickers show it as a progress bar),
/// <see cref="ReadingChanged"/> when it moved. The component URLs come from the manifest index (sent with
/// <c>BungieApiKey</c> when set), else from the pinned content id (<c>ManifestContentId</c> in wwwroot/appsettings.json).
/// It remembers the hashes it was asked for in this tab, so the designer doesn't read again what Home just read (or
/// failed to read).
/// </summary>
public sealed class BungieManifestJoining(BrowserInterop browser, string apiKey, string pinnedContentId)
{
    /// <summary>How many more times a component file whose download broke is downloaded again, from its start.</summary>
    public const int Retries = 3;

    private readonly SemaphoreSlim oneAtATime = new(1, 1);

    /// <summary>Whether the tab holds the manifest's components (they stay once in).</summary>
    private bool loaded;

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
    /// The startup load, once per tab (another call while it runs, or after it, does nothing): the manifest's components,
    /// then the listing of every weapon and armor piece, both kept for the tab; <see cref="Startup"/> says where it is.
    /// </summary>
    public async Task LoadAtStartupAsync()
    {
        if (Startup is not ManifestStartup.Waiting)
        {
            return;
        }

        ShowStartup(ManifestStartupShaping.DownloadBegun);                                      // impure (tab state)
        await oneAtATime.WaitAsync();                                                            // impure
        try
        {
            var files = await LoadFilesAsync();                                                  // impure
            var listed = files is Result<Unit, string>.Error failed
                ? new Result<ItemListing, string>.Error(failed.Failure)
                : await ListAllItemsAsync(ReportStartupRound);                                   // impure
            KeepListing(listed);                                                                 // impure (tab state)
            var ended = ManifestStartupShaping.EndStartup(listed);                               // pure
            ShowStartup(ended);                                                                  // impure (tab state)
        }
        finally
        {
            oneAtATime.Release();                                                                // impure
        }
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
        }
    }

    /// <summary>How many bytes of the manifest's components have come, unpacked, and which try the file coming is on (manifest.js, a few times a second).</summary>
    [JSInvokable]
    public void ReportDownload(double bytes, int attempt)
    {
        var download = ManifestProgressShaping.ToDownload((long)bytes, attempt, Retries);        // pure
        var startup = ManifestStartupShaping.ReportDownload(Startup, download);                  // pure
        var reading = Reading.Map(progress => ManifestProgressShaping.ReportDownload(progress, download)); // pure
        if (Startup is ManifestStartup.Downloading)
        {
            ShowStartup(startup);                                                                // impure (tab state)
        }

        if (Reading.IsSome())
        {
            ShowReading(reading);                                                                // impure (tab state)
        }
    }

    /// <summary>The manifest's components, downloaded and indexed when the tab doesn't hold them yet (then kept); or why they couldn't be.</summary>
    private async Task<Result<Unit, string>> LoadFilesAsync()
    {
        if (loaded)
        {
            return new Result<Unit, string>.Ok(new Unit.Value());
        }

        using var reporter = DotNetObjectReference.Create(this);                                 // impure
        var index = await browser.ReadManifestIndexAsync(LoopDesigning.ManifestIndexUrl, apiKey); // impure
        var urls = LoopDesigning.ChooseManifestUrls(index, pinnedContentId);                     // pure
        var files = await browser.LoadManifestTablesAsync(urls, Retries, reporter);              // impure
        var holding = files is Result<Unit, string>.Ok;                                          // pure
        loaded = holding;                                                                        // impure (tab state)
        return files;
    }

    private async Task<Result<ManifestExcerpt, string>> ReadAsync(ImmutableArray<ItemHash> hashes, ManifestReadFor readFor)
    {
        var started = loaded
            ? ManifestProgressShaping.StartNaming(readFor, hashes.Length)
            : ManifestProgressShaping.StartReading(readFor, hashes.Length);                      // pure
        ShowReading(Optional.Some(started));                                                     // impure (tab state)
        var files = await LoadFilesAsync();                                                      // impure
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
        var started = loaded
            ? ManifestProgressShaping.StartNaming(ManifestReadFor.EveryItem, 0)
            : ManifestProgressShaping.StartReading(ManifestReadFor.EveryItem, 0);                // pure
        ShowReading(Optional.Some(started));                                                     // impure (tab state)
        var files = await LoadFilesAsync();                                                      // impure
        return files is Result<Unit, string>.Error failed
            ? new Result<ItemListing, string>.Error(failed.Failure)
            : await ListAllItemsAsync(ReportReadingRound);                                       // impure
    }

    /// <summary>
    /// Every weapon and armor piece, from the components the tab holds: the listing's questions, each answered by the page
    /// (<paramref name="reportRound"/> is told which one, before the browser gets its turn), until it is done.
    /// </summary>
    private async Task<Result<ItemListing, string>> ListAllItemsAsync(Action<int> reportRound)
    {
        Result<ListingStep, string> step = new Result<ListingStep, string>.Ok(LoopDesigning.StartListing()); // pure
        while (step is Result<ListingStep, string>.Ok { Value: ListingStep.Ask ask })
        {
            var round = LoopDesigning.ToListingRound(ask);                                       // pure
            reportRound(round);                                                                  // impure (tab state)
            await Task.Delay(1);                                                                 // impure (the browser's turn: taps, the bar)
            var entries = await browser.ReadManifestEntriesAsync(ask.Query.Entries);            // impure
            var parts = await browser.ReadManifestPartsAsync(ask.Query.Parts);                  // impure
            step = LoopDesigning.ContinueListing(ask, entries, parts);                           // pure
        }

        return LoopDesigning.ToItemListing(step);                                                // pure
    }

    private void ReportStartupRound(int round)
    {
        var listing = ManifestStartupShaping.ReportListing(round, LoopDesigning.ListingRounds); // pure
        ShowStartup(listing);                                                                    // impure (tab state)
    }

    private void ReportReadingRound(int round)
    {
        var naming = Reading.Map(progress => ManifestProgressShaping.ReportRound(progress, round)); // pure
        ShowReading(naming);                                                                     // impure (tab state)
    }

    /// <summary>A listing read is kept for the tab; a failed one leaves none (the next opening tries again).</summary>
    private void KeepListing(Result<ItemListing, string> read)
    {
        var listing = read.Match(ok => Optional.Some(ok.Value), _ => Optional.None<ItemListing>()); // pure
        Listing = listing;                                                                       // impure (tab state)
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
