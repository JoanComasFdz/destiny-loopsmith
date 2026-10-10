using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.ManifestJoining;
using Loopsmith.Core.Orchestration;

namespace Loopsmith.Web.Hosting;

/// <summary>
/// Impure shell: Bungie's full manifest for the hashes the bundled excerpt lacks (a pasted DIM link's other items).
/// The components download from bungie.net the first time (the browser's HTTP cache keeps them; the app stores
/// nothing, ADRs D25) and stay in the tab; Core asks for their entries round by round (each round asks only what it
/// hasn't asked, so the rounds end) and builds the excerpt. The component URLs come from the manifest index (sent
/// with <c>BungieApiKey</c> when set), else from the pinned content id (<c>ManifestContentId</c> in
/// wwwroot/appsettings.json). It remembers the hashes it was asked for in this tab, so the designer doesn't read again
/// what Home just read (or failed to read).
/// </summary>
public sealed class BungieManifestJoining(BrowserInterop browser, string apiKey, string pinnedContentId)
{
    /// <summary>The hashes the manifest was read for in this tab (found, missing or failed).</summary>
    public ImmutableHashSet<ItemHash> Asked { get; private set; } = [];

    /// <summary>The excerpt of what <paramref name="hashes"/> need, from the full manifest; or why it couldn't be read.</summary>
    public async Task<Result<ManifestExcerpt, string>> JoinAsync(ImmutableArray<ItemHash> hashes)
    {
        var asked = Asked.Union(hashes);                                                         // pure
        Asked = asked;                                                                           // impure (tab state)
        var index = await browser.ReadManifestIndexAsync(LoopDesigning.ManifestIndexUrl, apiKey); // impure
        var urls = LoopDesigning.ChooseManifestUrls(index, pinnedContentId);                     // pure
        var loaded = await browser.LoadManifestTablesAsync(urls);                                // impure
        var step = loaded.Map(_ => LoopDesigning.StartManifestJoin(hashes));                     // pure
        while (step is Result<ManifestJoinStep, string>.Ok { Value: ManifestJoinStep.Ask ask })
        {
            var request = LoopDesigning.ToManifestRequest(ask.Query);                            // pure
            var search = LoopDesigning.ToManifestPlugSearch(ask.Query);                          // pure
            var entries = await browser.ReadManifestEntriesAsync(request);                       // impure
            var found = await browser.FindManifestEntriesContainingAsync(search);                // impure
            step = LoopDesigning.ContinueManifestJoin(ask, entries, found);                      // pure
        }

        return LoopDesigning.ToJoinedExcerpt(step);                                              // pure
    }
}
