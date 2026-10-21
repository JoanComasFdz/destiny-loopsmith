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
        var snapshot = SnapshotFiles.ReadSnapshot(folder);   // impure
        if (snapshot is Result<SnapshotContents, string>.Error unreadable)
        {
            return new Result<Unit, string>.Error(unreadable.Failure);
        }

        var meta = ((Result<SnapshotContents, string>.Ok)snapshot).Value.Meta;
        SnapshotFiles.CreateFolder(folder, "images");   // impure
        var mapJson = SnapshotFiles.ReadText(folder, Path.Combine("images", "map.json"));   // impure
        var savedBefore = mapJson is Optional<string>.Some json ? ImageListing.ReadImageMap(json.Value) : new Result<ImmutableArray<(string Url, string File)>, string>.Ok([]);   // pure
        if (savedBefore is Result<ImmutableArray<(string Url, string File)>, string>.Error badMap)
        {
            return new Result<Unit, string>.Error(badMap.Failure);
        }

        var saved = new ImageMap(((Result<ImmutableArray<(string Url, string File)>, string>.Ok)savedBefore).Value);
        var rawPages = SnapshotFiles.ReadRawPages(folder);   // impure
        var urls = ImageListing.ListImageUrls(rawPages);   // pure
        var todo = ImageListing.ListImagesToDownload(urls, saved.ToDictionary());   // pure
        if (todo.IsEmpty)
        {
            log.Say($"Images: all {urls.Length} already downloaded");   // impure
            return new Result<Unit, string>.Ok(new Unit.Value());
        }

        var referer = $"{SheetNaming.BuildTabListUrl(meta.Id)}";   // pure
        if (freshClient && meta.Id.Length > 0)
        {
            await client.GetAsync(referer, [], ImageTimeout);   // impure: picks up the cookies Google sets for a viewer
        }

        var fetching = new ImageFetching(client, folder, log, referer);
        var remaining = await ProbeHosts(fetching, todo, saved);   // impure
        log.Say($"Images: downloading {remaining.Length} of {urls.Length} ({AtOnce} at a time) ...");   // impure
        var progress = new Progress("Images", remaining.Length);   // impure
        fetching = fetching with { Progress = Optional.Some(progress) };
        var failed = ImmutableArray.CreateBuilder<FailedImage>();
        var finished = 0;
        await Parallel.ForEachAsync(remaining, new ParallelOptions { MaxDegreeOfParallelism = AtOnce }, async (url, _) =>
        {
            var image = await fetching.FetchImage(url);   // impure
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
                    SnapshotFiles.WriteText(folder, Path.Combine("images", "map.json"), resumable);   // impure
                    log.Info($"progress {finished}/{remaining.Length} ok={progress.Saved} fail={progress.Failed}");   // impure
                }
            }
        });
        progress.Close();   // impure
        var map = ImageListing.WriteImageMap(saved.ToList());   // pure
        SnapshotFiles.WriteText(folder, Path.Combine("images", "map.json"), map);   // impure
        log.Say($"Images: {saved.Count} saved, {failed.Count} failed");   // impure
        if (failed.Count > 0)
        {
            var failures = failed.ToImmutable();
            var failedList = ImageListing.WriteFailedList(failures);   // pure
            SnapshotFiles.WriteText(folder, Path.Combine("images", "failed.txt"), failedList);   // impure
            var summary = ImageListing.SummarizeFailures(failures);   // pure
            foreach (var line in summary)
            {
                log.Say(line);   // impure
            }

            log.Say("  (see images/failed.txt; failed ones stay as remote links; re-run with --rebuild to retry only those)");   // impure
        }

        return new Result<Unit, string>.Ok(new Unit.Value());
    }

    /// <summary>One image of each host first, so a host that never answers doesn't cost minutes of retries: its images are skipped.</summary>
    private static async Task<ImmutableArray<string>> ProbeHosts(ImageFetching fetching, ImmutableArray<string> todo, ImageMap saved)
    {
        var left = todo;
        foreach (var host in todo.Select(ImageListing.ReadHost).Distinct().Order(StringComparer.Ordinal))
        {
            var first = todo.First(url => ImageListing.ReadHost(url) == host);
            var image = await fetching.FetchImage(first);   // impure
            if (image is Result<string, FailedImage>.Ok file)
            {
                saved.Add(first, file.Value);
            }
            else if (ImageListing.IsHostUnreachable(((Result<string, FailedImage>.Error)image).Failure.Error))
            {
                var error = ((Result<string, FailedImage>.Error)image).Failure.Error;
                fetching.Log.Say($"Images: {host} not usable ({PythonText.TakeCharacters(error, 100)}); keeping remote links for it");   // impure
                left = [.. left.Where(url => ImageListing.ReadHost(url) != host)];
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

/// <summary>Impure: one image asked for, again after a wait when Google is busy, until it is saved or given up.</summary>
internal sealed record ImageFetching(SheetClient Client, string Folder, SnapshotLog Log, string Referer)
{
    public Optional<Progress> Progress { get; init; } = Optional.None<Progress>();

    public async Task<Result<string, FailedImage>> FetchImage(string url)
    {
        var error = "?";
        for (var attempt = 1; attempt <= ImageListing.Attempts; attempt++)
        {
            var answered = await Client.GetAsync(url, [("Referer", Referer), ("Accept", "image/avif,image/webp,image/png,image/*,*/*;q=0.8")], TimeSpan.FromSeconds(60));   // impure
            var next = answered is Result<SheetAnswer, string>.Ok answer
                ? ReadAnswer(url, attempt, answer.Value)   // impure
                : ReadError(url, attempt, ((Result<SheetAnswer, string>.Error)answered).Failure);   // impure
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
                Log.Info($"img wait {again.WaitSeconds}s before retry | {url}");   // impure
                ChangeWaiting(+1);   // impure
            }

            await Task.Delay(TimeSpan.FromSeconds(again.WaitSeconds));   // impure
            if (next.RateLimited)
            {
                ChangeWaiting(-1);   // impure
            }
        }

        Log.Error($"img GAVE UP: {error} | {url}");   // impure
        return new Result<string, FailedImage>.Error(new FailedImage(error, url));
    }

    private Attempted ReadAnswer(string url, int attempt, SheetAnswer answer)
    {
        var mediaType = ImageListing.ReadMediaType(answer.ContentType);   // pure
        var described = ResponseDescribing.DescribeAnswer(answer.Facts);   // pure
        if (answer.Status < 400 && ImageListing.IsImage(mediaType))
        {
            var name = ImageListing.NameImageFile(url, mediaType);   // pure
            SnapshotFiles.WriteBytes(Folder, Path.Combine("images", name), answer.Body);   // impure
            Log.Debug($"img OK   try{attempt} {name} -> {described} | {url}");   // impure
            return new Attempted(Optional.Some(name), "", new ImageRetry.GiveUp(), false);
        }

        var body = ImageListing.IsImage(mediaType) ? "" : PythonText.TakeCharacters(answer.Text, 300).Replace('\n', ' ');   // pure
        Log.Warn($"img FAIL try{attempt} {described} | body={PythonText.QuoteForConsole(body)} | {url}");   // impure
        var retry = ImageListing.DecideAfterAnswer(answer.Status, attempt, answer.RetryAfter);   // pure
        return new Attempted(Optional.None<string>(), $"HTTP {answer.Status} {mediaType}", retry, true);
    }

    private Attempted ReadError(string url, int attempt, string error)
    {
        Log.Warn($"img EXC  try{attempt} {error} | {url}");   // impure
        var retry = ImageListing.DecideAfterError(attempt);   // pure
        return new Attempted(Optional.None<string>(), PythonText.TakeCharacters(error, 150), retry, false);
    }

    private void ChangeWaiting(int change)
    {
        if (Progress is Optional<Progress>.Some progress)
        {
            progress.Value.Wait(change);
        }
    }

    /// <summary>How one attempt went: the file saved, or the error, what to do next and whether Google asked to wait.</summary>
    private sealed record Attempted(Optional<string> Saved, string Error, ImageRetry Retry, bool RateLimited);
}
