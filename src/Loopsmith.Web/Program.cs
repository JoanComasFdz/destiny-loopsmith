using Loopsmith.Core.Functional;
using Loopsmith.Web;
using Loopsmith.Web.Hosting;
using Loopsmith.Web.State;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

// Host only: read the bundled files (impure), parse the catalog once (pure), then hand the result to the UI.
var builder = WebAssemblyHostBuilder.CreateDefault(args);                                  // impure
builder.RootComponents.Add<App>("#app");                                                   // impure
builder.RootComponents.Add<HeadOutlet>("head::after");                                     // impure

var files = EmbeddedDataReading.ReadEmbeddedFiles(typeof(App).Assembly);                   // impure

var startup = files.Bind(BundleComposing.ComposeBundle);                                   // pure
var shareLinksWork = ShareLinkCoding.CheckRoundTrip();                                     // pure
var workbench = new Workbench(startup, shareLinksWork);                                    // pure
var configuredKey = builder.Configuration["DimApiKey"];                                    // impure (wwwroot/appsettings.json)
var dimApiKey = (configuredKey ?? "").Trim();                                              // pure
var dimLoadouts = new DimLoadoutFetching(new HttpClient(), dimApiKey);                     // impure
var configuredBungieKey = builder.Configuration["BungieApiKey"];                           // impure (wwwroot/appsettings.json)
var configuredContentId = builder.Configuration["ManifestContentId"];                      // impure (wwwroot/appsettings.json)
var bungieApiKey = (configuredBungieKey ?? "").Trim();                                     // pure
var manifestContentId = (configuredContentId ?? "").Trim();                                // pure

builder.Services.AddSingleton(workbench);                                                  // impure
builder.Services.AddSingleton<BrowserInterop>();                                           // impure
builder.Services.AddSingleton(dimLoadouts);                                                // impure
builder.Services.AddSingleton(services => new BungieManifestJoining(services.GetRequiredService<BrowserInterop>(), bungieApiKey, manifestContentId)); // impure
await builder.Build().RunAsync();                                                          // impure
