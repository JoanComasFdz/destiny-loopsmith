namespace Loopsmith.CompendiumDownload.Hosting;

/// <summary>
/// Impure boundary: the snapshot's <c>download.log</c> (every request, its status and errors, appended per run) and the
/// console. Safe to use from the image downloads running side by side.
/// </summary>
public sealed class SnapshotLog
{
    private readonly string path;
    private readonly Lock writing = new();

    private SnapshotLog(string path)
    {
        this.path = path;
    }

    /// <summary>The folder's log, created with the folder, its run's command line first.</summary>
    public static SnapshotLog OpenLog(string folder, IReadOnlyList<string> args)
    {
        Directory.CreateDirectory(folder);
        var log = new SnapshotLog(Path.Combine(folder, "download.log"));
        log.LogInfo(new string('=', 70));
        log.LogInfo($"run: Loopsmith.CompendiumDownload {string.Join(' ', args)}");
        return log;
    }

    public string LogPath => path;

    /// <summary>A line on the console, and in the log.</summary>
    public void Say(string message)
    {
        lock (writing)
        {
            Console.WriteLine(message);
        }

        LogInfo(message);
    }

    public void LogDebug(string message) => Write("DEBUG", message);

    public void LogInfo(string message) => Write("INFO", message);

    public void LogWarning(string message) => Write("WARNING", message);

    public void LogError(string message) => Write("ERROR", message);

    private void Write(string level, string message)
    {
        lock (writing)
        {
            File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss,fff} {level,-7} [thread {Environment.CurrentManagedThreadId}] {message}\n");
        }
    }
}
