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
/// only what it hasn't asked, so the rounds end) and builds the excerpt; then the components are let go (they are most
/// of the tab's memory). The page keeps answering taps meanwhile: the download pauses every few megabytes, and the join
/// gives the browser a turn before each round. <see cref="Reading"/> says how far a read got (the Bungie login section
/// shows it as a progress bar), <see cref="ReadingChanged"/> when it moved. The component URLs come from the manifest
/// index (sent with <c>BungieApiKey</c> when set), else from the pinned content id (<c>ManifestContentId</c> in
/// wwwroot/appsettings.json). It remembers the hashes it was asked for in this tab, so the designer doesn't read again
/// what Home just read (or failed to read).
/// </summary>
public sealed class BungieManifestJoining(BrowserInterop browser, string apiKey, string pinnedContentId)
{
    /// <summary>The hashes the manifest was read for in this tab (found, missing or failed).</summary>
    public ImmutableHashSet<ItemHash> Asked { get; private set; } = [];

    /// <summary>The read in progress, when there is one.</summary>
    public Optional<ManifestReadProgress> Reading { get; private set; } = Optional.None<ManifestReadProgress>();

    /// <summary>The read in progress moved on (or ended).</summary>
    public event Action? ReadingChanged;

    /// <summary>The excerpt of what <paramref name="hashes"/> need, from the full manifest; or why it couldn't be read.</summary>
    public async Task<Result<ManifestExcerpt, string>> JoinAsync(ImmutableArray<ItemHash> hashes)
    {
        var asked = Asked.Union(hashes);                                                         // pure
        Asked = asked;                                                                           // impure (tab state)
        var started = ManifestProgressShaping.StartReading(hashes.Length);                        // pure
        ShowReading(Optional.Some(started));                                                     // impure (tab state)
        using var reporter = DotNetObjectReference.Create(this);                                 // impure
        var index = await browser.ReadManifestIndexAsync(LoopDesigning.ManifestIndexUrl, apiKey); // impure
        var urls = LoopDesigning.ChooseManifestUrls(index, pinnedContentId);                     // pure
        var loaded = await browser.LoadManifestTablesAsync(urls, reporter);                      // impure
        var step = loaded.Map(_ => LoopDesigning.StartManifestJoin(hashes));                     // pure
        var round = 0;
        while (step is Result<ManifestJoinStep, string>.Ok { Value: ManifestJoinStep.Ask ask })
        {
            round++;
            var request = LoopDesigning.ToManifestRequest(ask.Query);                            // pure
            var search = LoopDesigning.ToManifestPlugSearch(ask.Query);                          // pure
            var entries = await browser.ReadManifestEntriesAsync(request);                       // impure
            var found = await browser.FindManifestEntriesContainingAsync(search);                // impure
            var naming = Reading.Map(reading => ManifestProgressShaping.ReportRound(reading, round)); // pure
            ShowReading(naming);                                                                 // impure (tab state)
            await Task.Delay(1);                                                                 // impure (the browser's turn: taps, the bar)
            step = LoopDesigning.ContinueManifestJoin(ask, entries, found);                      // pure
        }

        await browser.ReleaseManifestTablesAsync();                                              // impure
        ShowReading(Optional.None<ManifestReadProgress>());                                           // impure (tab state)
        return LoopDesigning.ToJoinedExcerpt(step);                                              // pure
    }

    /// <summary>How many bytes of the manifest's components have come (manifest.js, a few times a second).</summary>
    [JSInvokable]
    public void ReportDownload(double bytes)
    {
        var moved = Reading.Map(reading => ManifestProgressShaping.ReportDownload(reading, (long)bytes)); // pure
        ShowReading(moved);                                                                      // impure (tab state)
    }

    private void ShowReading(Optional<ManifestReadProgress> reading)
    {
        Reading = reading;                                                                       // impure (tab state)
        ReadingChanged?.Invoke();                                                                // impure
    }
}
