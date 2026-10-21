using Dunet;
using Loopsmith.Functional;

namespace Loopsmith.ManifestTools.Core;

/// <summary>What the tool does: download a manifest, or write the excerpt from one.</summary>
[Union]
public partial record ManifestCommand
{
    /// <summary>Download the components into <see cref="Folder"/>, with its version.txt.</summary>
    partial record Download(string Folder);

    /// <summary>
    /// Write <c>&lt;Root&gt;/rules/manifest.yaml</c> from the manifest in <see cref="Folder"/>; its version is
    /// <see cref="Version"/>, or else the folder's version.txt.
    /// </summary>
    partial record Extract(string Folder, Optional<string> Version, string Root);
}

/// <summary>
/// The command line: <c>download &lt;dir&gt;</c> or <c>extract &lt;dir&gt; [--version &lt;version&gt;] [--root &lt;repo&gt;]</c>.
/// </summary>
public static class OptionsReading
{
    public const string Usage =
        "usage: dotnet run --project tools/manifest/Loopsmith.ManifestTools -- (download <dir> | extract <dir> [--version <version>] [--root <repo>])";

    public static Result<ManifestCommand, string> ReadOptions(IReadOnlyList<string> args) =>
        args.ToArray() switch
        {
            ["download", var folder] => new Result<ManifestCommand, string>.Ok(new ManifestCommand.Download(folder)),
            ["extract", var folder, .. var flags] when !folder.StartsWith("--", StringComparison.Ordinal) =>
                ReadFlags([.. flags], 0, new ManifestCommand.Extract(folder, Optional.None<string>(), ".")).Map(ManifestCommand (extract) => extract),
            ["download" or "extract", ..] => new Result<ManifestCommand, string>.Error($"Give the manifest folder. {Usage}"),
            _ => new Result<ManifestCommand, string>.Error($"The command is 'download' or 'extract'. {Usage}"),
        };

    private static Result<ManifestCommand.Extract, string> ReadFlags(IReadOnlyList<string> flags, int at, ManifestCommand.Extract given) =>
        at >= flags.Count
            ? new Result<ManifestCommand.Extract, string>.Ok(given)
            : flags[at] switch
            {
                "--version" or "--root" when at + 1 >= flags.Count =>
                    new Result<ManifestCommand.Extract, string>.Error($"'{flags[at]}' needs a value. {Usage}"),
                "--version" => ReadFlags(flags, at + 2, given with { Version = Optional.Some(flags[at + 1]) }),
                "--root" => ReadFlags(flags, at + 2, given with { Root = flags[at + 1] }),
                _ => new Result<ManifestCommand.Extract, string>.Error($"Unknown option '{flags[at]}'. {Usage}"),
            };
}
