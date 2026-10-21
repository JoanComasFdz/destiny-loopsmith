using System.Collections.Immutable;
using Loopsmith.E2ETools.Core;
using Loopsmith.Functional;

namespace Loopsmith.E2ETools.Tests;

/// <summary>A recording's runs → its scenarios.json (what post reads), the rules to draw and the summary record.sh prints.</summary>
public sealed class ScenarioListingTests
{
    private static readonly RunRecord Rule = new(
        "Rules/Hunter/Arc/Aspects", "LethalCurrentTests", "Arms", "Arms the melee",
        Optional.Some("# Rule hunter-arc-aspects-lethal-current-2: arms\n  # Rule hunter-arc-aspects-lethal-current-3: also\nScenario: arms"),
        "desktop", "arms-desktop", Optional.Some(2.345), true);

    private static readonly RunRecord Loop = new(
        "Starting", "StartFromScratchTests", "Opens", "Opens a build", Optional.None<string>(), "mobile", "opens-mobile", Optional.None<double>(), false);

    [Fact]
    public void The_rules_to_draw_are_each_named_rule_once_per_domain_folder()
    {
        var mobile = Rule with { Screen = "mobile", Gif = "arms-mobile" };

        Assert.Equal(
            ["Rules/Hunter/Arc/Aspects hunter-arc-aspects-lethal-current-2", "Rules/Hunter/Arc/Aspects hunter-arc-aspects-lethal-current-3"],
            ScenarioListing.ListFlows([Rule, mobile, Loop]).Select(ScenarioListing.WriteFlowLine));
    }

    [Fact]
    public void The_selection_keeps_the_changed_classes_and_each_fixture_only_class_s_fixtures_sorted_once()
    {
        var selection = ScenarioListing.ReadSelection("A\tchanged\nB\tfixtures\tz.yaml\nB\tfixtures\ta.yaml\nB\tfixtures\ta.yaml\n\n");

        Assert.Equal(["A"], selection.Changed);
        Assert.Equal(["a.yaml", "z.yaml"], selection.Fixtures["B"]);
    }

    [Fact]
    public void Scenarios_json_holds_each_run_with_its_selection_and_its_rules_flow()
    {
        var selection = ScenarioListing.ReadSelection("LethalCurrentTests\tfixtures\tScenarios/a.build.yaml\n");
        var flows = ImmutableDictionary<FlowToDraw, DrawnFlow>.Empty
            .Add(new FlowToDraw("Rules/Hunter/Arc/Aspects", "hunter-arc-aspects-lethal-current-2"), new DrawnFlow(true, Optional.None<string>()));

        var json = ScenarioListing.WriteScenarios([Rule, Loop], selection, flows);

        Assert.Equal(
            """
            [
              {
                "domain": "Rules/Hunter/Arc/Aspects",
                "class": "LethalCurrentTests",
                "method": "Arms",
                "title": "Arms the melee",
                "gherkin": "# Rule hunter-arc-aspects-lethal-current-2: arms\n  # Rule hunter-arc-aspects-lethal-current-3: also\nScenario: arms",
                "screen": "desktop",
                "gif": "Rules/Hunter/Arc/Aspects/arms-desktop.gif",
                "mp4": "Rules/Hunter/Arc/Aspects/arms-desktop.mp4",
                "startupSeconds": 2.3,
                "passed": true,
                "selected": "fixtures",
                "fixtures": [
                  "Scenarios/a.build.yaml"
                ],
                "rule": "hunter-arc-aspects-lethal-current-2",
                "flow": "Rules/Hunter/Arc/Aspects/hunter-arc-aspects-lethal-current-2.png"
              },
              {
                "domain": "Starting",
                "class": "StartFromScratchTests",
                "method": "Opens",
                "title": "Opens a build",
                "gherkin": null,
                "screen": "mobile",
                "gif": "Starting/opens-mobile.gif",
                "mp4": "Starting/opens-mobile.mp4",
                "startupSeconds": null,
                "passed": false,
                "selected": "changed"
              }
            ]
            """.ReplaceLineEndings("\n"),
            json.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Scenarios_json_is_what_post_reads()
    {
        var flows = ImmutableDictionary<FlowToDraw, DrawnFlow>.Empty
            .Add(new FlowToDraw("Rules/Hunter/Arc/Aspects", "hunter-arc-aspects-lethal-current-2"), new DrawnFlow(false, Optional.Some("No rule is named 'x'")));

        var entries = Assert.IsType<Result<ImmutableArray<ScenarioEntry>, string>.Ok>(
            JsonReading.ReadScenarios(ScenarioListing.WriteScenarios([Rule, Loop], ScenarioListing.ReadSelection(""), flows))).Value;

        Assert.Equal(Optional.Some("No rule is named 'x'"), entries[0].FlowError);
        Assert.Equal(Optional.None<string>(), entries[0].Flow);
        Assert.Equal(Optional.Some("Starting/opens-mobile.mp4"), entries[1].Mp4);
        Assert.IsType<RunSelection.Changed>(entries[1].Selection);
    }

    [Fact]
    public void A_rule_with_neither_png_nor_error_is_not_drawn() =>
        Assert.Contains("\"flowError\": \"not drawn\"", ScenarioListing.WriteScenarios([Rule], ScenarioListing.ReadSelection(""), ImmutableDictionary<FlowToDraw, DrawnFlow>.Empty));

    [Fact]
    public void No_run_is_an_empty_list() =>
        Assert.Equal("[]", ScenarioListing.WriteScenarios([], ScenarioListing.ReadSelection(""), ImmutableDictionary<FlowToDraw, DrawnFlow>.Empty));

    [Fact]
    public void The_summary_gives_each_runs_result_startup_and_gif_and_why_it_was_re_recorded() =>
        Assert.Equal(
            [
                "",
                "Scenarios:",
                "  passed     startup 2.3 s  Rules/Hunter/Arc/Aspects/arms-desktop.gif  (re-recorded: a.yaml, b.yaml changed)",
                "  FAILED  Home never shown  Starting/opens-mobile.gif",
            ],
            ScenarioListing.WriteSummary([Rule, Loop], ScenarioListing.ReadSelection("LethalCurrentTests\tfixtures\tb.yaml\nLethalCurrentTests\tfixtures\ta.yaml\nStartFromScratchTests\tchanged\nStartFromScratchTests\tfixtures\tc.yaml\n")));
}
