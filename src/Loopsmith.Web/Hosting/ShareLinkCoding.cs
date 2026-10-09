using System.Buffers.Text;
using System.IO.Compression;
using System.Text;
using Loopsmith.Core.Functional;

namespace Loopsmith.Web.Hosting;

/// <summary>
/// Share links (docs/loop-format.md): <c>…/#loop=&lt;payload&gt;</c>, payload = base64url (no padding) of the
/// raw-DEFLATE-compressed UTF-8 loop file. In-memory only (no I/O), so every function here is pure; it lives in
/// the web host because the core's architecture tests keep <c>System.IO</c> out of the core.
/// </summary>
public static class ShareLinkCoding
{
    public const string FragmentKey = "loop=";

    /// <summary>A decoded loop file larger than this is refused (a link is not a file-sharing service).</summary>
    private const int MaxDecodedBytes = 2 * 1024 * 1024;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>Produced by Python's <c>zlib.compressobj(9, DEFLATED, -15)</c>: proves we read other encoders' payloads.</summary>
    private const string KnownPayload = "y8nPL7BSCM7IzMtPKn3UMLNYISy_XOFRwxSFw9PNFB5N28BVXJJaUGylEB3LBQA";
    private const string KnownText = "loop: Shinobu’s Vow — ×6 ▰\nsteps: []\n";

    public static string EncodeLoopPayload(string loopFile)
    {
        using var compressed = new MemoryStream();
        using (var deflate = new DeflateStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            deflate.Write(StrictUtf8.GetBytes(loopFile));
        }

        return Base64Url.EncodeToString(compressed.ToArray());
    }

    public static Result<string, string> DecodeLoopPayload(string payload)
    {
        try
        {
            var bytes = Base64Url.DecodeFromChars(payload.Trim());
            using var input = new MemoryStream(bytes);
            using var deflate = new DeflateStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            var buffer = new byte[16 * 1024];
            int read;
            while ((read = deflate.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (output.Length + read > MaxDecodedBytes)
                {
                    return new Result<string, string>.Error("The shared loop is too large (over 2 MB once decompressed).");
                }

                output.Write(buffer, 0, read);
            }

            return new Result<string, string>.Ok(StrictUtf8.GetString(output.ToArray()));
        }
        catch (Exception exception) when (exception is FormatException or InvalidDataException or DecoderFallbackException)
        {
            return new Result<string, string>.Error($"The link's loop payload is damaged ({exception.Message}).");
        }
    }

    /// <summary>The share URL for a loop file: the app's base URI with the payload in the fragment.</summary>
    public static string ToShareUrl(string baseUri, string loopFile) =>
        $"{baseUri}#{FragmentKey}{EncodeLoopPayload(loopFile)}";

    /// <summary>The payload of a <c>#loop=</c> fragment in <paramref name="uri"/>, if there is one.</summary>
    public static Optional<string> ReadLoopPayload(string uri)
    {
        var hash = uri.IndexOf('#', StringComparison.Ordinal);
        var fragment = hash < 0 ? "" : uri[(hash + 1)..];
        var entry = fragment.Split('&').FirstOrDefault(part => part.StartsWith(FragmentKey, StringComparison.Ordinal), "");
        return entry.Length > FragmentKey.Length
            ? Optional.Some(Uri.UnescapeDataString(entry[FragmentKey.Length..]))
            : Optional.None<string>();
    }

    /// <summary>Startup self-check (no unit tests in the host): our round trip, and a payload from another encoder.</summary>
    public static bool CheckRoundTrip()
    {
        var ours = DecodeLoopPayload(EncodeLoopPayload(KnownText)).Match(ok => ok.Value == KnownText, _ => false);
        var theirs = DecodeLoopPayload(KnownPayload).Match(ok => ok.Value == KnownText, _ => false);
        return ours && theirs;
    }
}
