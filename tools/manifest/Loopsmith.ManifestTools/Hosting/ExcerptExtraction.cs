using System.Collections.Immutable;
using Loopsmith.Functional;
using Loopsmith.ManifestTools.Core;

namespace Loopsmith.ManifestTools.Hosting;

/// <summary><c>extract</c>: the manifest folder and the repository's rules → rules/manifest.yaml.</summary>
public static class ExcerptExtraction
{
    public static Result<Unit, string> ExtractExcerpt(ManifestCommand.Extract command)
    {
        Result<string, string> versionText = new Result<string, string>.Ok(command.Version.UnwrapOr(""));   // pure
        if (command.Version is Optional<string>.None)
        {
            versionText = ManifestFiles.ReadVersionFile(command.Folder);   // impure
        }

        var version = versionText.Bind(text => ManifestDownloading.ReadVersionFile(text).ToResult(() => "The manifest's version.txt is empty: pass --version."));   // pure
        var ruleTexts = ManifestFiles.ReadRuleTexts(command.Root);   // impure
        var testItems = ManifestFiles.ReadTestItems(command.Root);   // impure
        var components = ManifestFiles.ReadComponents(command.Folder);   // impure
        var named = ruleTexts.Bind(texts => testItems.Bind(yaml => NamedHashReading.ReadNamedHashes(texts, yaml)));   // pure
        var json = named.Bind(_ => components);   // pure
        var tables = json.Bind(files => files.Select(file => ManifestTableParsing.ParseTable(file.Name, file.Json)).CombineAll().MapError(failures => string.Join("\n", failures)));   // pure
        var manifest = tables.Bind(ManifestTableParsing.ToComponents);   // pure
        var excerpt = manifest.Bind(components => named.Bind(hashes => version.Bind(written => ExcerptWriting.WriteExcerpt(components, hashes, written))));   // pure
        if (excerpt is not Result<Excerpt, string>.Ok written)
        {
            return excerpt.Map(Unit (_) => new Unit.Value());
        }

        var saved = ManifestFiles.WriteExcerpt(command.Root, written.Value.Text);   // impure
        if (saved is Result<Unit, string>.Error)
        {
            return saved;
        }

        ImmutableArray<string> missing = [.. written.Value.Missing.Select(hash => $"not in the manifest: {hash}")];   // pure
        ManifestFiles.Report(written.Value.Summary);   // impure
        ManifestFiles.ReportMissing(missing);   // impure
        return saved;
    }
}
