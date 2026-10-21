using Loopsmith.E2ETools.Core;
using Loopsmith.StaticServer.Hosting;

namespace Loopsmith.E2ETools.Tests;

/// <summary>The static server record.sh serves the published site with, as GitHub Pages and Python's http.server serve it.</summary>
public sealed class StaticServerTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"static-server-{Guid.NewGuid():N}");
    private Microsoft.AspNetCore.Builder.WebApplication? _server;
    private readonly HttpClient _client = new(new HttpClientHandler { AllowAutoRedirect = false });

    public async ValueTask InitializeAsync()
    {
        Directory.CreateDirectory(Path.Combine(_root, "destiny-loopsmith", "_framework"));   // impure
        await File.WriteAllTextAsync(Path.Combine(_root, "destiny-loopsmith", "index.html"), "<html>home</html>");   // impure
        await File.WriteAllTextAsync(Path.Combine(_root, "destiny-loopsmith", "_framework", "icudt.dat"), "bytes");   // impure
        _server = StaticSiteServing.BuildServer(new ServerOptions(_root, 0));   // impure
        await _server.StartAsync();   // impure
        _client.BaseAddress = new Uri(StaticSiteServing.FindAddress(_server));
    }

    public async ValueTask DisposeAsync()
    {
        await _server!.DisposeAsync();   // impure
        _client.Dispose();
        Directory.Delete(_root, recursive: true);   // impure
    }

    [Fact]
    public async Task A_folder_serves_its_index_html()
    {
        var answer = await _client.GetAsync("/destiny-loopsmith/", TestContext.Current.CancellationToken);   // impure

        Assert.Equal(System.Net.HttpStatusCode.OK, answer.StatusCode);
        Assert.Equal("text/html", answer.Content.Headers.ContentType?.MediaType);
        Assert.Equal("<html>home</html>", await answer.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));   // impure
    }

    [Fact]
    public async Task A_folder_asked_without_its_slash_is_redirected_to_it()
    {
        var answer = await _client.GetAsync("/destiny-loopsmith", TestContext.Current.CancellationToken);   // impure

        Assert.Equal(System.Net.HttpStatusCode.MovedPermanently, answer.StatusCode);
        Assert.EndsWith("/destiny-loopsmith/", answer.Headers.Location?.ToString());
    }

    [Fact]
    public async Task A_file_of_a_type_it_doesnt_know_is_served_as_bytes_and_a_missing_one_is_404()
    {
        var data = await _client.GetAsync("/destiny-loopsmith/_framework/icudt.dat", TestContext.Current.CancellationToken);   // impure
        var missing = await _client.GetAsync("/destiny-loopsmith/designer", TestContext.Current.CancellationToken);   // impure

        Assert.Equal("application/octet-stream", data.Content.Headers.ContentType?.MediaType);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, missing.StatusCode);
    }
}
