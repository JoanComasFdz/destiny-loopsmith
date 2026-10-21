using Loopsmith.E2ETools.Core;
using Loopsmith.Functional;
using Loopsmith.StaticServer.Hosting;
using Microsoft.Extensions.Hosting;

// Impure → pure → impure: read the folder to serve, serve it on 127.0.0.1 and print the address (record.sh waits for that
// line), until stopped (tools/e2e/README.md).
var options = ServerOptionsReading.ReadServerOptions(args);   // pure
if (options is Result<ServerOptions, string>.Error badOptions)
{
    Console.Error.WriteLine(badOptions.Failure);   // impure
    return 2;
}

var given = ((Result<ServerOptions, string>.Ok)options).Value;
if (!Directory.Exists(given.Root))   // impure
{
    Console.Error.WriteLine($"No folder {given.Root} to serve.");   // impure
    return 2;
}

var server = StaticSiteServing.BuildServer(given);   // impure
await server.StartAsync();   // impure
var address = StaticSiteServing.FindAddress(server);   // impure
var line = ServerOptionsReading.WriteListeningLine(address);   // pure
Console.WriteLine(line);   // impure
await server.WaitForShutdownAsync();   // impure
return 0;
