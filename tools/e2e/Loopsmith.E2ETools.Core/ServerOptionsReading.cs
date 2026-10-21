using Loopsmith.Functional;

namespace Loopsmith.E2ETools.Core;

/// <summary>What the static server serves: a folder, on 127.0.0.1 at a port (none: one the system picks).</summary>
public sealed record ServerOptions(string Root, Optional<int> Port);

/// <summary>The static server's command line (tools/e2e/Loopsmith.StaticServer) → its options, or what is wrong with it.</summary>
public static class ServerOptionsReading
{
    public const string Usage = "usage: dotnet run --project tools/e2e/Loopsmith.StaticServer -- <folder> [--port <n>]";

    public static Result<ServerOptions, string> ReadServerOptions(IReadOnlyList<string> args) =>
        args.ToArray() switch
        {
            [var root] => new Result<ServerOptions, string>.Ok(new ServerOptions(root, Optional.None<int>())),
            [var root, "--port", var port] when int.TryParse(port, out var number) && number is >= 0 and <= 65535 =>
                new Result<ServerOptions, string>.Ok(new ServerOptions(root, Optional.Some(number))),
            _ => new Result<ServerOptions, string>.Error(Usage),
        };

    /// <summary>The line the server prints once it listens, which record.sh waits for: its address.</summary>
    public static string WriteListeningLine(string address) => address.EndsWith('/') ? address : $"{address}/";
}
