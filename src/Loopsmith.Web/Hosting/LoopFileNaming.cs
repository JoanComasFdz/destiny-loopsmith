namespace Loopsmith.Web.Hosting;

/// <summary>Pure: the file name an exported loop is downloaded as.</summary>
public static class LoopFileNaming
{
    /// <summary><c>&lt;name&gt;.loop.yaml</c>, the name reduced to a safe slug ("loop" when nothing is left).</summary>
    public static string ToLoopFileName(string name)
    {
        var slug = new string(name.Trim().ToLowerInvariant().Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-').ToArray());
        var collapsed = string.Join('-', slug.Split('-', StringSplitOptions.RemoveEmptyEntries));
        return $"{(collapsed.Length == 0 ? "loop" : collapsed)}.loop.yaml";
    }
}
