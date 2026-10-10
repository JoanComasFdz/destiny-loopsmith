using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;

namespace Loopsmith.Web.Hosting;

/// <summary>
/// Impure shell: Bungie's full manifest for the hashes the bundled excerpt lacks (a pasted DIM link's other items).
/// The components download from bungie.net the first time (the browser's HTTP cache keeps them; the app stores
/// nothing, ADRs D25) and stay in the tab; Core asks for their entries round by round and builds the excerpt.
/// The component paths come from the manifest index (sent with <c>BungieApiKey</c> when set), else from the
/// pinned content id (<c>ManifestContentId</c> in wwwroot/appsettings.json).
/// </summary>
public sealed class BungieManifestJoining(BrowserInterop browser, string apiKey, string pinnedContentId)
{
    private const string Host = "https://www.bungie.net";
    private const string IndexUrl = Host + "/Platform/Destiny2/Manifest/";
    private const int MaxRounds = 12;

    /// <summary>The excerpt of what <paramref name="hashes"/> need, from the full manifest; or why it couldn't be read.</summary>
    public async Task<Result<ManifestExcerpt, string>> JoinAsync(ImmutableArray<ItemHash> hashes)
    {
        var index = await browser.ReadManifestIndexAsync(IndexUrl, apiKey);                       // impure
        var paths = LoopDesigning.ChooseManifestPaths(index, pinnedContentId);                   // pure
        var urls = paths.ToImmutableDictionary(entry => LoopDesigning.ToManifestComponentName(entry.Key), entry => Host + entry.Value); // pure
        var problem = await browser.LoadManifestTablesAsync(urls);                               // impure
        if (problem.Length > 0)
        {
            return new Result<ManifestExcerpt, string>.Error($"Couldn't read Bungie's manifest ({problem}).");
        }

        var items = LoopDesigning.ToManifestComponentName(ManifestTable.InventoryItem);          // pure
        var step = LoopDesigning.StartManifestJoin(hashes);                                      // pure
        for (var round = 0; step is ManifestJoinStep.Ask ask && round < MaxRounds; round++)
        {
            var request = LoopDesigning.ToManifestRequest(ask.Query);                            // pure
            var entries = await browser.ReadManifestEntriesAsync(request);                       // impure
            var plugs = await browser.FindManifestPlugsAsync(items, ask.Query.PlugCategories);   // impure
            var answer = LoopDesigning.ReadManifestAnswer(entries, plugs);                       // pure
            step = LoopDesigning.ContinueManifestJoin(ask.Progress, answer);                     // pure
        }

        return step is ManifestJoinStep.Done done
            ? new Result<ManifestExcerpt, string>.Ok(done.Excerpt)
            : new Result<ManifestExcerpt, string>.Error("Bungie's manifest answered, but reading it didn't finish.");
    }
}
