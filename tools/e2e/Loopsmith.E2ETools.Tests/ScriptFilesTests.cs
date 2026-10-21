using System.Collections.Immutable;
using Loopsmith.E2ETools.Core;
using Loopsmith.Functional;

namespace Loopsmith.E2ETools.Tests;

/// <summary>The files the e2e scripts hand the tool: a run's record, a runner's package.json, scenarios.json's files, the .vsix.</summary>
public sealed class ScriptFilesTests
{
    [Fact]
    public void A_runs_record_is_read_with_its_home_time_or_none()
    {
        var json = """
            {"domain": "Starting", "class": "StartFromScratchTests", "method": "Opens", "title": "Opens", "gherkin": "Scenario: x",
             "screen": "desktop", "gif": "opens-desktop", "homeShownSeconds": 3.25, "passed": true}
            """;

        var record = Assert.IsType<Result<RunRecord, string>.Ok>(JsonReading.ReadRunRecord(json)).Value;

        Assert.Equal(new RunRecord("Starting", "StartFromScratchTests", "Opens", "Opens", Optional.Some("Scenario: x"), "desktop", "opens-desktop", Optional.Some(3.25), true), record);
        Assert.Equal(Optional.None<double>(), Assert.IsType<Result<RunRecord, string>.Ok>(JsonReading.ReadRunRecord(json.Replace("3.25", "null"))).Value.HomeShownSeconds);
    }

    [Fact]
    public void A_record_without_its_names_is_an_error() =>
        Assert.IsType<Result<RunRecord, string>.Error>(JsonReading.ReadRunRecord("""{"domain": "Starting"}"""));

    [Fact]
    public void The_VS_Code_build_is_the_package_json_s_config_vscodeCommit()
    {
        Assert.Equal("abc123", Assert.IsType<Result<string, string>.Ok>(JsonReading.ReadVsCodeCommit("""{"config": {"vscodeCommit": "abc123"}}""")).Value);
        Assert.IsType<Result<string, string>.Error>(JsonReading.ReadVsCodeCommit("""{"config": {}}"""));
    }

    [Fact]
    public void The_listed_files_are_each_gif_mp4_and_flow_once_in_order()
    {
        var json = """
            [{"gif": "R/a-desktop.gif", "mp4": "R/a-desktop.mp4", "flow": "R/rule.png"},
             {"gif": "R/a-mobile.gif", "mp4": "R/a-mobile.mp4", "flow": "R/rule.png"},
             {"gif": "S/b-desktop.gif", "mp4": null}]
            """;

        Assert.Equal(
            ["R/a-desktop.gif", "R/a-desktop.mp4", "R/rule.png", "R/a-mobile.gif", "R/a-mobile.mp4", "S/b-desktop.gif"],
            Assert.IsType<Result<ImmutableArray<string>, string>.Ok>(JsonReading.ReadListedFiles(json)).Value);
        Assert.Empty(Assert.IsType<Result<ImmutableArray<string>, string>.Ok>(JsonReading.ReadListedFiles("[]")).Value);
    }

    [Fact]
    public void The_vsix_lays_the_extension_out_as_the_sites_is() =>
        Assert.Equal(
            [
                new VsixEntry("ext/package.json", "extension/package.json"),
                new VsixEntry("ext/extension.js", "extension/extension.js"),
                new VsixEntry("ext/README.md", "extension/README.md"),
                new VsixEntry("LICENSE", "extension/LICENSE.txt"),
                new VsixEntry("ext/vsix/extension.vsixmanifest", "extension.vsixmanifest"),
                new VsixEntry("ext/vsix/Content_Types.xml", "[Content_Types].xml"),
            ],
            VsixPacking.PlanEntries("ext", "LICENSE"));
}
