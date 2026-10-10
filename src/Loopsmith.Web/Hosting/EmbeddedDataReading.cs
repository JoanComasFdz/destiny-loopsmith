using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Web.Hosting;

/// <summary>
/// Impure boundary of the web host: the rule, build, saved DIM share and loop files embedded in this assembly (see the csproj),
/// read once at startup. Each becomes a <see cref="SourceText"/> named by its forward-slash logical name
/// (<c>rules/glossary.yaml</c>, <c>builds/skip-grenade-hunter/build.yaml</c>).
/// </summary>
public static class EmbeddedDataReading
{
    public static Result<ImmutableArray<SourceText>, string> ReadEmbeddedFiles(Assembly assembly)
    {
        try
        {
            return assembly.GetManifestResourceNames()
                .Where(name => name.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.Ordinal)
                .Select(name => ReadEmbeddedFile(assembly, name))
                .CombineAll()
                .MapError(errors => string.Join(Environment.NewLine, errors));
        }
        catch (Exception exception) when (exception is IOException or BadImageFormatException)
        {
            return new Result<ImmutableArray<SourceText>, string>.Error($"Cannot read the bundled data: {exception.Message}");
        }
    }

    private static Result<SourceText, string> ReadEmbeddedFile(Assembly assembly, string name)
    {
        using var stream = assembly.GetManifestResourceStream(name);
        if (stream is null)
        {
            return new Result<SourceText, string>.Error($"Bundled file '{name}' is missing.");
        }

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return new Result<SourceText, string>.Ok(new SourceText(name, reader.ReadToEnd()));
    }
}
