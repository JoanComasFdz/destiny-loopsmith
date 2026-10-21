using Loopsmith.E2ETools.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;

namespace Loopsmith.StaticServer.Hosting;

/// <summary>
/// Impure boundary: a folder served over HTTP on 127.0.0.1, as GitHub Pages serves the site: a folder's index.html for the
/// folder (a folder asked without its slash is redirected to it), every other file as it is, by its extension's type or as
/// bytes, and 404 for what isn't there. Only warnings are logged.
/// </summary>
public static class StaticSiteServing
{
    public static WebApplication BuildServer(ServerOptions options)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.WebHost.UseUrls($"http://127.0.0.1:{options.Port}");
        var server = builder.Build();
        var files = new PhysicalFileProvider(Path.GetFullPath(options.Root));
        server.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
        server.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = files,
            ContentTypeProvider = new FileExtensionContentTypeProvider(),
            ServeUnknownFileTypes = true,
            DefaultContentType = "application/octet-stream",
        });
        return server;
    }

    /// <summary>The address the server listens on, once started (with port 0, the port the system picked).</summary>
    public static string FindAddress(WebApplication server) =>
        server.Services.GetRequiredService<IServer>().Features.GetRequiredFeature<IServerAddressesFeature>().Addresses.First();
}
