using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using static Loopsmith.Core.RuleParsing.ResultAccumulation;
using Errors = System.Collections.Immutable.ImmutableArray<Loopsmith.Core.RuleParsing.ParseError>;

namespace Loopsmith.Core.RuleParsing;

/// <summary>
/// The rule set (<c>rules/**/*.yaml</c>, see <c>docs/rule-format.md</c>) → <see cref="RuleCatalog"/>. Pure.
/// </summary>
public static class RuleCatalogParsing
{
    /// <summary>
    /// Parses the glossary, then every elements file against it; checks every status, pickup and summon
    /// reference (and whether a buff or a debuff belongs in that position), that element ids (and
    /// manifest hashes) are unique across files and that <c>doesNotStackWith</c> names other elements without leading back to its own
    /// (ADRs D6). Every problem of the file set is reported at once, one <c>file:line: message</c> per line. If the
    /// glossary itself is missing or broken, the element files are still parsed for their own errors, but references
    /// are not checked against it.
    /// </summary>
    /// <param name="files">Every rule file, with its path relative to the rules root (<c>glossary.yaml</c>, <c>keywords/arc.yaml</c>).</param>
    public static Result<RuleCatalog, string> ParseCatalog(ImmutableArray<SourceText> files)
    {
        var ordered = files.OrderBy(file => file.Path, StringComparer.Ordinal).ToImmutableArray();
        var glossary = ReadTheGlossary(ordered);
        var scope = new ReferenceScope(glossary.Match(ok => Optional.Some(ok.Value), _ => Optional.None<KeywordGlossary>()));
        var manifest = ReadTheManifest(ordered);
        var elementFiles = ordered
            .Where(file => !IsGlossary(file) && !IsManifest(file))
            .Select(file => ElementParsing.ReadElementsFile(file, scope))
            .ToImmutableArray();
        var fileErrors = FailIfAny(elementFiles.SelectMany(file => file.Errors).ToImmutableArray());
        var located = elementFiles.SelectMany(file => file.Elements).ToImmutableArray();
        var elements = LocatedCollections.ToUniqueDictionary(located, element => element.Id, "element");
        var stacking = ReferenceChecking.CheckStackingReferences(located);
        var hashes = ReferenceChecking.CheckUniqueHashes(located);
        var version = ComputeCatalogVersion(ordered);
        return Combine(glossary, fileErrors, elements, stacking, hashes, manifest, (vocabulary, _, byId, _, _, excerpt) => new RuleCatalog(version, vocabulary, byId, excerpt))
            .MapError(FormatErrors);
    }

    /// <summary>
    /// <c>authored-</c> + the first 12 lowercase hex chars of SHA-256 over the (path, text) pairs sorted by
    /// path (ordinal), each pair encoded as UTF-8(path) 0x00 UTF-8(text) 0x00.
    /// </summary>
    internal static CatalogVersion ComputeCatalogVersion(ImmutableArray<SourceText> files)
    {
        var canonical = files
            .OrderBy(file => file.Path, StringComparer.Ordinal)
            .ThenBy(file => file.Text, StringComparer.Ordinal)
            .SelectMany(ToCanonicalBytes)
            .ToArray();
        var digest = Convert.ToHexStringLower(SHA256.HashData(canonical));

        // Never blank, so CatalogVersion's validation always passes: this From cannot throw.
        return CatalogVersion.From($"authored-{digest[..12]}");
    }

    private static byte[] ToCanonicalBytes(SourceText file) =>
        [.. Encoding.UTF8.GetBytes(file.Path), 0, .. Encoding.UTF8.GetBytes(file.Text), 0];

    private static bool IsGlossary(SourceText file) =>
        file.Path == GlossaryParsing.GlossaryPath;

    private static bool IsManifest(SourceText file) =>
        file.Path == ManifestParsing.ManifestPath;

    /// <summary>The manifest excerpt, if the rules root has one (it is optional: without it nothing has a name or icon from the manifest).</summary>
    private static Result<ManifestExcerpt, Errors> ReadTheManifest(ImmutableArray<SourceText> ordered) =>
        ordered.Where(IsManifest).ToImmutableArray() switch
        {
            [] => Succeed(ManifestParsing.NoManifest),
            [var manifest] => ManifestParsing.ReadManifest(manifest),
            var several => Fail<ManifestExcerpt>(
                [new ParseError(ManifestParsing.ManifestPath, Optional.None<long>(), $"given {several.Length} times: the rules root has at most one manifest.yaml")]),
        };

    private static Result<KeywordGlossary, Errors> ReadTheGlossary(ImmutableArray<SourceText> ordered) =>
        ordered.Where(IsGlossary).ToImmutableArray() switch
        {
            [] => Fail<KeywordGlossary>(ToFileErrors("missing: the rules root must contain exactly one glossary.yaml")),
            [var glossary] => GlossaryParsing.ReadGlossary(glossary),
            var several => Fail<KeywordGlossary>(
                ToFileErrors($"given {several.Length} times: the rules root must contain exactly one glossary.yaml")),
        };

    private static Errors ToFileErrors(string message) =>
        [new ParseError(GlossaryParsing.GlossaryPath, Optional.None<long>(), message)];

    private static string FormatErrors(Errors errors) =>
        string.Join(
            "\n",
            errors
                .Distinct()
                .OrderBy(error => error.Path, StringComparer.Ordinal)
                .ThenBy(error => error.Line.UnwrapOr(0))
                .Select(FormatError));

    private static string FormatError(ParseError error) =>
        error.Line.Match(
            line => $"{error.Path}:{line.Value}: {error.Message}",
            _ => $"{error.Path}: {error.Message}");
}
