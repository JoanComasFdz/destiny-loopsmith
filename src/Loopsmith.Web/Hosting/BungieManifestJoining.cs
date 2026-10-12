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
/// login's armor and weapons). The components download from bungie.net (the browser's HTTP cache keeps them; the app
/// stores nothing, ADRs D25) and are indexed as they come; Core asks for their entries round by round (each round asks
/// only what it hasn't asked, so the rounds end) and builds the excerpt; then, when no other read waits for them, the
/// components are let go (they are most of the tab's memory). One read runs at a time: another (the page answers taps
/// meanwhile, so a login can come during a DIM link's read) waits for it. The page keeps answering taps: the download
/// pauses every few megabytes, and the join gives the browser a turn before each round. <see cref="Reading"/> says how
/// far the read got (the Bungie login section and the ⋯ pickers show it as a progress bar), <see cref="ReadingChanged"/>
/// when it moved. The component URLs come from the manifest index (sent with <c>BungieApiKey</c> when set), else from
/// the pinned content id (<c>ManifestContentId</c> in wwwroot/appsettings.json). It remembers the hashes it was asked
/// for in this tab, so the designer doesn't read again what Home just read (or failed to read).
/// <para>
/// It also lists every legendary and exotic weapon and armor piece of the game, for the pickers without a login: read
/// once per tab and kept (a few thousand items: little memory). While such a picker is open it holds the components, so
/// the item picked is read at once; once the last one closes, they go after <see cref="KeepAfterPicker"/> (a player
/// often fills several slots in a row).
/// </para>
/// </summary>
public sealed class BungieManifestJoining(BrowserInterop browser, string apiKey, string pinnedContentId)
{
    /// <summary>How long the components stay after the last picker that holds them closes.</summary>
    private static readonly TimeSpan KeepAfterPicker = TimeSpan.FromMinutes(1);

    private readonly SemaphoreSlim oneAtATime = new(1, 1);
    private int waiting;
    private int holds;
    private int releases;

    /// <summary>The hashes the manifest was read for in this tab (found, missing or failed).</summary>
    public ImmutableHashSet<ItemHash> Asked { get; private set; } = [];

    /// <summary>The game's weapons and armor, once listed in this tab.</summary>
    public Optional<GameItems> Listed { get; private set; } = Optional.None<GameItems>();

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

    /// <summary>
    /// Every legendary and exotic weapon and armor piece of the game (the pickers without a login): listed from the full
    /// manifest the first time, then as kept; or why it couldn't be read (the next call tries again).
    /// </summary>
    public async Task<Result<GameItems, string>> ListGameItemsAsync()
    {
        if (Listed is Optional<GameItems>.Some kept)
        {
            return new Result<GameItems, string>.Ok(kept.Value);
        }

        waiting++;                                                                               // impure (tab state)
        await oneAtATime.WaitAsync();                                                            // impure
        waiting--;                                                                               // impure (tab state)
        try
        {
            if (Listed is Optional<GameItems>.Some meanwhile)
            {
                return new Result<GameItems, string>.Ok(meanwhile.Value);
            }

            var listed = await ReadGameItemsAsync();                                             // impure
            var keeping = listed.Match(ok => Optional.Some(ok.Value), _ => Optional.None<GameItems>()); // pure
            Listed = keeping;                                                                    // impure (tab state)
            return listed;
        }
        finally
        {
            await ReleaseAsync();                                                                // impure
            oneAtATime.Release();                                                                // impure
        }
    }

    /// <summary>A picker that lists the game's items opened: the components stay after each read until it closes.</summary>
    public void Hold()
    {
        holds++;                                                                                 // impure (tab state)
        releases++;                                                                              // impure (tab state: a release on its way is off)
    }

    /// <summary>
    /// A picker that held the components closed: once none holds them and none has opened for <see cref="KeepAfterPicker"/>
    /// (and no read runs or waits), they go.
    /// </summary>
    public async Task LetGoAsync()
    {
        holds--;                                                                                 // impure (tab state)
        releases++;                                                                              // impure (tab state)
        var release = releases;
        await Task.Delay(KeepAfterPicker);                                                       // impure
        if (release == releases && holds == 0 && waiting == 0 && oneAtATime.CurrentCount == 1)
        {
            await browser.ReleaseManifestTablesAsync();                                          // impure
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

    /// <summary>The listing's rounds: the components (as for a join), then each question answered until it is done.</summary>
    private async Task<Result<GameItems, string>> ReadGameItemsAsync()
    {
        var started = ManifestProgressShaping.StartReading(ManifestReadFor.GameItems, 0);        // pure
        ShowReading(Optional.Some(started));                                                     // impure (tab state)
        using var reporter = DotNetObjectReference.Create(this);                                 // impure
        var index = await browser.ReadManifestIndexAsync(LoopDesigning.ManifestIndexUrl, apiKey); // impure
        var urls = LoopDesigning.ChooseManifestUrls(index, pinnedContentId);                     // pure
        var loaded = await browser.LoadManifestTablesAsync(urls, reporter);                      // impure
        var step = loaded.Map(_ => LoopDesigning.StartGameItemListing());                        // pure
        while (step is Result<ManifestListStep, string>.Ok { Value: ManifestListStep.Ask ask })
        {
            var request = LoopDesigning.ToGameListRequest(ask.Query);                            // pure
            var outlineRequest = LoopDesigning.ToGameListOutlineRequest(ask.Query);              // pure
            var search = LoopDesigning.ToGameListSearch(ask.Query);                              // pure
            var entries = await browser.ReadManifestEntriesAsync(request);                       // impure
            var outlines = await browser.ReadManifestOutlinesAsync(outlineRequest, ask.Query.OutlinePaths); // impure
            var found = await browser.FindManifestEntriesContainingAsync(search);                // impure
            var naming = Reading.Map(ManifestProgressShaping.ReportRound);                       // pure
            ShowReading(naming);                                                                 // impure (tab state)
            await Task.Delay(1);                                                                 // impure (the browser's turn: taps, the bar)
            step = LoopDesigning.ContinueGameItemListing(ask, entries, outlines, found);         // pure
        }

        return LoopDesigning.ToListedGameItems(step);                                            // pure
    }

    /// <summary>The read is over (done or failed): the bar goes, and the components, unless another read waits for them or a picker holds them.</summary>
    private async Task ReleaseAsync()
    {
        ShowReading(Optional.None<ManifestReadProgress>());                                      // impure (tab state)
        if (waiting == 0 && holds == 0)
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
