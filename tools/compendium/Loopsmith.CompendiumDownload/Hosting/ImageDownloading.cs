using System.Collections.Immutable;
using Loopsmith.CompendiumDownload.Core;
using Loopsmith.Functional;

namespace Loopsmith.CompendiumDownload.Hosting;

/// <summary>
/// Impure: every image the tabs use saved into <c>images/</c>, 4 at a time, so the viewer works offline. Google's image
/// links load only when asked from one of its pages (its Referer and cookies), which is why they break in a viewer opened
/// from disk. Those already saved (<c>images/map.json</c>) aren't asked again.
/// </summary>
public static class ImageDownloading
{
    private const int AtOnce = 4;
    private static readonly TimeSpan ImageTimeout = TimeSpan.FromSeconds(60);

    public static async Task<Result<Unit, string>> DownloadImages(SheetClient client, bool freshClient, string folder, SnapshotLog log)
    {
        var read = SnapshotFiles.ReadSnapshotFolder(folder);   // impure
        var opened = read.Bind(SnapshotOpening.OpenSnapshot);   // pure
        var mapJson = SnapshotFiles.ReadText(folder, Path.Combine("images", "map.json"));   // impure
        var savedBefore = mapJson.Bind(ReadImageMap);   // pure
        var rawPages = SnapshotFiles.ReadRawPages(folder);   // impure
        var started = opened.Bind(snapshot => savedBefore.Bind(map => rawPages.Map(pages => (snapshot.Meta, Map: map, Pages: pages))));   // pure
        if (started is Result<(SheetMeta Meta, ImmutableArray<(string Url, string File)> Map, ImmutableArray<string> Pages), string>.Error unreadable)
        {
            return new Result<Unit, string>.Error(unreadable.Failure);
        }

        var (meta, before, pages) = ((Result<(SheetMeta Meta, ImmutableArray<(string Url, string File)> Map, ImmutableArray<string> Pages), string>.Ok)started).Value;
        SnapshotFiles.CreateFolder(folder, "images");   // impure
        var saved = new ImageMap(before);
        var urls = ImageListing.ListImageUrls(pages);   // pure
        var todo = ImageListing.ListImagesToDownload(urls, saved.ToDictionary());   // pure
        if (todo.IsEmpty)
        {
            log.Say($"Images: all {urls.Length} already downloaded");   // impure
            return new Result<Unit, string>.Ok(new Unit.Value());
        }

        var referer = SheetNaming.BuildTabListUrl(meta.Id);   // pure
        if (freshClient && meta.Id.Length > 0)
        {
            await client.GetAsync(referer, [], ImageTimeout);   // impure: picks up the cookies Google sets for a viewer
        }

        var probing = new ImageFetch(client, folder, log, referer, Optional.None<Progress>());
        var remaining = await ProbeHosts(probing, todo, saved);   // impure
        log.Say($"Images: downloading {remaining.Length} of {urls.Length} ({AtOnce} at a time) ...");   // impure
        var progress = new Progress("Images", remaining.Length);   // impure
        var fetching = probing with { Progress = Optional.Some(progress) };
        var failed = await FetchAll(fetching, remaining, saved, progress);   // impure
        progress.Close();   // impure
        var map = ImageListing.WriteImageMap(saved.ToList());   // pure
        SnapshotFiles.WriteText(folder, Path.Combine("images", "map.json"), map);   // impure
        log.Say($"Images: {saved.Count} saved, {failed.Length} failed");   // impure
        if (!failed.IsEmpty)
        {
            var failedList = ImageListing.WriteFailedList(failed);   // pure
            SnapshotFiles.WriteText(folder, Path.Combine("images", "failed.txt"), failedList);   // impure
            var summary = ImageListing.SummarizeFailures(failed);   // pure
            foreach (var line in summary)
            {
                log.Say(line);   // impure
            }

            log.Say("  (see images/failed.txt; failed ones stay as remote links; re-run with --rebuild to retry only those)");   // impure
        }

        return new Result<Unit, string>.Ok(new Unit.Value());
    }

    private static Result<ImmutableArray<(string Url, string File)>, string> ReadImageMap(Optional<string> mapJson) =>
        mapJson is Optional<string>.Some json ? ImageListing.ReadImageMap(json.Value) : new Result<ImmutableArray<(string Url, string File)>, string>.Ok([]);

    /// <summary>The images left, 4 at a time; map.json written every 100, so a stopped run resumes. The ones given up.</summary>
    private static async Task<ImmutableArray<FailedImage>> FetchAll(ImageFetch fetching, ImmutableArray<string> remaining, ImageMap saved, Progress progress)
    {
        var failed = ImmutableArray.CreateBuilder<FailedImage>();
        var finished = 0;
        await Parallel.ForEachAsync(remaining, new ParallelOptions { MaxDegreeOfParallelism = AtOnce }, async (url, _) =>
        {
            var image = await ImageFetching.FetchImage(fetching, url);   // impure
            lock (failed)
            {
                if (image is Result<string, FailedImage>.Ok file)
                {
                    saved.Add(url, file.Value);
                }
                else
                {
                    failed.Add(((Result<string, FailedImage>.Error)image).Failure);
                }

                finished++;
                progress.Step(image is Result<string, FailedImage>.Ok);   // impure
                if (finished % 100 == 0)
                {
                    var resumable = ImageListing.WriteImageMap(saved.ToList());   // pure
                    SnapshotFiles.WriteText(fetching.Folder, Path.Combine("images", "map.json"), resumable);   // impure
                    fetching.Log.LogInfo($"progress {finished}/{remaining.Length} ok={progress.Saved} fail={progress.Failed}");   // impure
                }
            }
        });
        return failed.ToImmutable();
    }

    /// <summary>One image of each host first, so a host that never answers doesn't cost minutes of retries: its images are skipped.</summary>
    private static async Task<ImmutableArray<string>> ProbeHosts(ImageFetch fetching, ImmutableArray<string> todo, ImageMap saved)
    {
        var left = todo;
        var probes = ImageListing.ListHostProbes(todo);   // pure
        foreach (var (host, url) in probes)
        {
            var image = await ImageFetching.FetchImage(fetching, url);   // impure
            if (image is Result<string, FailedImage>.Ok file)
            {
                saved.Add(url, file.Value);
                continue;
            }

            var error = ((Result<string, FailedImage>.Error)image).Failure.Error;
            var unreachable = ImageListing.IsHostUnreachable(error);   // pure
            if (unreachable)
            {
                var shown = PythonText.TakeCharacters(error, 100);   // pure
                fetching.Log.Say($"Images: {host} not usable ({shown}); keeping remote links for it");   // impure
                left = ImageListing.DropHost(left, host);   // pure
            }
        }

        return [.. left.Where(url => !saved.Contains(url))];
    }

    /// <summary>The images saved so far, in the order they were saved (map.json keeps it).</summary>
    private sealed class ImageMap(ImmutableArray<(string Url, string File)> before)
    {
        private readonly List<(string Url, string File)> entries = [.. before.DistinctBy(entry => entry.Url)];

        public int Count => entries.Count;

        public bool Contains(string url) => entries.Any(entry => entry.Url == url);

        public void Add(string url, string file) => entries.Add((url, file));

        public IReadOnlyDictionary<string, string> ToDictionary() => entries.ToDictionary(entry => entry.Url, entry => entry.File);

        public IEnumerable<(string Url, string File)> ToList() => [.. entries];
    }
}

/// <summary>What fetching an image needs: the client, the folder its file goes in, the log, the page to say it is asked from and the progress line.</summary>
internal sealed record ImageFetch(SheetClient Client, string Folder, SnapshotLog Log, string Referer, Optional<Progress> Progress);

/// <summary>How one attempt went: the file saved, or the error, what to do next and whether Google asked to wait.</summary>
internal sealed record ImageAttempt(Optional<string> Saved, string Error, ImageRetry Retry, bool RateLimited);

/// <summary>Impure: one image asked for, again after a wait when Google is busy, until it is saved or given up.</summary>
internal static class ImageFetching
{
    public static async Task<Result<string, FailedImage>> FetchImage(ImageFetch fetching, string url)
    {
        var error = "?";
        for (var attempt = 1; attempt <= ImageListing.Attempts; attempt++)
        {
            var answered = await fetching.Client.GetAsync(url, [("Referer", fetching.Referer), ("Accept", "image/avif,image/webp,image/png,image/*,*/*;q=0.8")], TimeSpan.FromSeconds(60));   // impure
            var next = answered is Result<SheetAnswer, string>.Ok answer
                ? SaveAnswer(fetching, url, attempt, answer.Value)   // impure
                : LogError(fetching, url, attempt, ((Result<SheetAnswer, string>.Error)answered).Failure);   // impure
            if (next.Saved is Optional<string>.Some file)
            {
                return new Result<string, FailedImage>.Ok(file.Value);
            }

            error = next.Error;
            if (next.Retry is not ImageRetry.TryAgain again)
            {
                break;
            }

            if (next.RateLimited)
            {
                fetching.Log.LogInfo($"img wait {again.WaitSeconds}s before retry | {url}");   // impure
                ChangeWaiting(fetching, +1);   // impure
            }

            await Task.Delay(TimeSpan.FromSeconds(again.WaitSeconds));   // impure
            if (next.RateLimited)
            {
                ChangeWaiting(fetching, -1);   // impure
            }
        }

        fetching.Log.LogError($"img GAVE UP: {error} | {url}");   // impure
        return new Result<string, FailedImage>.Error(new FailedImage(error, url));
    }

    /// <summary>An image answer saved into images/; anything else logged, with what to do next.</summary>
    private static ImageAttempt SaveAnswer(ImageFetch fetching, string url, int attempt, SheetAnswer answer)
    {
        var mediaType = ImageListing.ReadMediaType(answer.ContentType);   // pure
        var described = ResponseDescribing.DescribeAnswer(answer.Facts);   // pure
        var savable = ImageListing.IsSavable(answer.Status, mediaType);   // pure
        if (savable)
        {
            var name = ImageListing.NameImageFile(url, mediaType);   // pure
            SnapshotFiles.WriteBytes(fetching.Folder, Path.Combine("images", name), answer.Body);   // impure
            fetching.Log.LogDebug($"img OK   try{attempt} {name} -> {described} | {url}");   // impure
            return new ImageAttempt(Optional.Some(name), "", new ImageRetry.GiveUp(), false);
        }

        var body = ImageListing.ReadFailedBody(mediaType, answer.Text);   // pure
        var quotedBody = PythonText.QuoteForConsole(body);   // pure
        fetching.Log.LogWarning($"img FAIL try{attempt} {described} | body={quotedBody} | {url}");   // impure
        var retry = ImageListing.DecideAfterAnswer(answer.Status, attempt, answer.RetryAfter);   // pure
        var failure = ImageListing.DescribeFailedAnswer(answer.Status, mediaType);   // pure
        return new ImageAttempt(Optional.None<string>(), failure, retry, true);
    }

    /// <summary>A request that got no answer, logged, with what to do next.</summary>
    private static ImageAttempt LogError(ImageFetch fetching, string url, int attempt, string error)
    {
        fetching.Log.LogWarning($"img EXC  try{attempt} {error} | {url}");   // impure
        var retry = ImageListing.DecideAfterError(attempt);   // pure
        var shown = PythonText.TakeCharacters(error, 150);   // pure
        return new ImageAttempt(Optional.None<string>(), shown, retry, false);
    }

    private static void ChangeWaiting(ImageFetch fetching, int change)
    {
        if (fetching.Progress is Optional<Progress>.Some progress)
        {
            progress.Value.Wait(change);
        }
    }
}
