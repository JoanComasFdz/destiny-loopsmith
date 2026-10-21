namespace Loopsmith.CompendiumDownload.Hosting;

/// <summary>Impure boundary: one live progress line on the console (redrawn at most 5 times a second), safe from several downloads at once.</summary>
public sealed class Progress
{
    private readonly string label;
    private readonly int total;
    private readonly DateTime started = DateTime.Now;
    private readonly Lock counting = new();
    private DateTime drawn = DateTime.MinValue;
    private int done;
    private int waiting;

    public Progress(string label, int total)
    {
        this.label = label;
        this.total = total;
        Draw(force: true);
    }

    public int Saved { get; private set; }

    public int Failed { get; private set; }

    public void Step(bool saved)
    {
        lock (counting)
        {
            done++;
            Saved += saved ? 1 : 0;
            Failed += saved ? 0 : 1;
        }

        Draw(force: done == total);
    }

    /// <summary>One more (or one fewer) download backing off after a rate limit.</summary>
    public void Wait(int change)
    {
        lock (counting)
        {
            waiting += change;
        }

        Draw(force: false);
    }

    public void Close()
    {
        Draw(force: true);
        Console.WriteLine();
    }

    private void Draw(bool force)
    {
        lock (counting)
        {
            var now = DateTime.Now;
            if (!force && now - drawn < TimeSpan.FromMilliseconds(200))
            {
                return;
            }

            drawn = now;
            var fraction = total > 0 ? (double)done / total : 1;
            var filled = (int)(fraction * 30);
            var elapsed = now - started;
            var left = done > 0 ? elapsed / done * (total - done) : TimeSpan.Zero;
            var backingOff = waiting > 0 ? $" | {waiting} backing off (rate limited)" : "";
            var line = $"\r{label} [{new string('#', filled)}{new string('-', 30 - filled)}] {done}/{total} {fraction,4:0%}"
                + $" | ok {Saved} fail {Failed} | {FormatTime(elapsed)} elapsed, ~{FormatTime(left)} left{backingOff}";
            Console.Write(line.PadRight(130)[..Math.Min(160, Math.Max(130, line.Length))]);
        }
    }

    private static string FormatTime(TimeSpan time) => $"{(int)time.TotalMinutes}m{time.Seconds:00}s";
}
