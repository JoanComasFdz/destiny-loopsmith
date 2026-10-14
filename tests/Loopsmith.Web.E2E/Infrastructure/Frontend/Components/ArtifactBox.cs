using Loopsmith.Web.E2E.Infrastructure.Vocabulary;
using Microsoft.Playwright;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>The Artifact box of the designer's loadout (Components/LoadoutPanel.razor).</summary>
public sealed class ArtifactBox(IPage page)
{
    private ILocator Name => page.Locator("[data-testid=artifact] [data-testid=artifact-item-name]");

    /// <summary>What the Artifact box shows now.</summary>
    public async Task<ArtifactBoxSnapshot> ReadAsync()
    {
        var named = await Name.CountAsync() > 0;
        var artifact = named ? ArtifactShowing.ReadArtifact((await Name.InnerTextAsync()).Trim()) : null;
        return new ArtifactBoxSnapshot(artifact);
    }
}

/// <summary>What the Artifact box shows: the artifact, or none ("No artifact").</summary>
public sealed record ArtifactBoxSnapshot(Artifact? Artifact);
