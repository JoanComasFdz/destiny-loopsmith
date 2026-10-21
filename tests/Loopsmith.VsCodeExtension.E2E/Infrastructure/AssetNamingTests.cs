namespace Loopsmith.VsCodeExtension.E2E.Infrastructure;

/// <summary>The fake release names its assets as the workflow does (runs without a browser).</summary>
public sealed class AssetNamingTests
{
    [Fact]
    public void AShortName_IsThePrTheVersionAndTheGifName() =>
        Assert.Equal(
            "pr-109-v1-start-from-scratch-opens-an-arc-hunter-with-nothing-equipped-desktop.mp4",
            AssetNaming.NameAsset(109, 1, "Starting", "start-from-scratch-opens-an-arc-hunter-with-nothing-equipped-desktop", ".mp4"));

    [Fact]
    public void ANamePastTwoHundredCharacters_KeepsItsStartAndAHashOfItsGifPath() =>
        // Written by tools/e2e's ReleaseNaming.NameAsset for the same GIF.
        Assert.Equal(
            "pr-112-v3-a-scenario-whose-title-goes-on-and-on-past-two-hundred-characters-still-finds-its-recording-because-the-workflow-shortens-its-asset-names-with-a-hash-and-the-extension-shortens-1732f032.mp4",
            AssetNaming.NameAsset(
                112,
                3,
                "Recordings",
                "a-scenario-whose-title-goes-on-and-on-past-two-hundred-characters-still-finds-its-recording-because-the-workflow-shortens-its-asset-names-with-a-hash-and-the-extension-shortens-them-the-same-way-desktop",
                ".mp4"));
}
