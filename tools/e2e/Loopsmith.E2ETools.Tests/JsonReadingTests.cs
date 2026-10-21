using Loopsmith.E2ETools.Core;
using Loopsmith.Functional;

namespace Loopsmith.E2ETools.Tests;

/// <summary>record.sh's scenarios.json and GitHub's answers, read into the tool's records.</summary>
public sealed class JsonReadingTests
{
    [Fact]
    public void A_scenarios_entry_keeps_its_fields_and_its_fixtures()
    {
        var json = """
            [{"domain": "Rules/Hunter/Arc/Aspects", "class": "LethalCurrentTests", "method": "Arms", "title": "Arms",
              "gherkin": "Scenario: arms", "screen": "desktop", "gif": "Rules/Hunter/Arc/Aspects/arms-desktop.gif",
              "mp4": "Rules/Hunter/Arc/Aspects/arms-desktop.mp4", "startupSeconds": 1.5, "passed": false,
              "selected": "fixtures", "fixtures": ["a.yaml"], "rule": "hunter-arc-aspects-lethal-current-2",
              "flow": "Rules/Hunter/Arc/Aspects/hunter-arc-aspects-lethal-current-2.png"}]
            """;

        var entry = Assert.Single(Unwrap(JsonReading.ReadScenarios(json)));

        Assert.Equal("LethalCurrentTests", entry.Class);
        Assert.Equal(Optional.Some("desktop"), entry.Screen);
        Assert.Equal(Optional.Some("hunter-arc-aspects-lethal-current-2"), entry.Rule);
        Assert.Equal(Optional.None<string>(), entry.FlowError);
        Assert.False(entry.Passed);
        Assert.Equal(["a.yaml"], Assert.IsType<RunSelection.Fixtures>(entry.Selection).Paths);
    }

    [Fact]
    public void A_scenarios_entry_without_selected_or_rule_reads_as_changed_with_no_flow()
    {
        var json = """[{"class": "ReloadTests", "method": "Reloads", "title": "T", "gherkin": null, "screen": "mobile", "gif": "Loop/r.gif", "mp4": null, "passed": true}]""";

        var entry = Assert.Single(Unwrap(JsonReading.ReadScenarios(json)));

        Assert.IsType<RunSelection.Changed>(entry.Selection);
        Assert.Equal(Optional.None<string>(), entry.Gherkin);
        Assert.Equal(Optional.None<string>(), entry.Mp4);
        Assert.Equal(Optional.None<string>(), entry.Rule);
    }

    [Fact]
    public void An_entry_missing_what_it_needs_is_an_error_saying_so()
    {
        var read = JsonReading.ReadScenarios("""[{"class": "ReloadTests"}]""");

        Assert.Equal("scenarios.json: an entry lacks its class, method, title, gif or passed", Assert.IsType<Result<System.Collections.Immutable.ImmutableArray<ScenarioEntry>, string>.Error>(read).Failure);
    }

    [Fact]
    public void Text_that_isnt_JSON_is_an_error_saying_what_was_read() =>
        Assert.StartsWith("the PR's comments isn't JSON", Assert.IsType<Result<System.Collections.Immutable.ImmutableArray<PrComment>, string>.Error>(JsonReading.ReadComments("<html>")).Failure);

    [Fact]
    public void Every_page_of_comments_is_read_and_a_null_body_is_empty()
    {
        var json = """[[{"id": 1, "node_id": "IC_1", "user": {"login": "github-actions[bot]"}, "body": "a"}], [{"id": 2, "node_id": "IC_2", "user": {"login": "x"}, "body": null}]]""";

        var comments = Unwrap(JsonReading.ReadComments(json));

        Assert.Equal([new PrComment(1, "IC_1", "github-actions[bot]", "a"), new PrComment(2, "IC_2", "x", "")], comments);
    }

    [Fact]
    public void The_hidden_posts_are_those_GraphQL_answers_minimized_in_the_order_asked()
    {
        var hidden = JsonReading.ReadHidden(["IC_1", "IC_2", "IC_3"], """{"data": {"nodes": [{"isMinimized": false}, {"isMinimized": true}, null]}}""");

        Assert.Equal(["IC_2"], Unwrap(hidden));
    }

    [Fact]
    public void A_GraphQL_answer_without_nodes_is_an_error_holding_the_answer() =>
        Assert.Equal("""{"errors": []}""", Assert.IsType<Result<System.Collections.Immutable.ImmutableHashSet<string>, string>.Error>(
            JsonReading.ReadHidden(["IC_1"], """{"errors": []}""")).Failure);

    [Fact]
    public void A_body_is_sent_as_JSON_whatever_it_holds()
    {
        var body = "a \"quoted\" & <b>\n🖥️";

        var sent = System.Text.Json.Nodes.JsonNode.Parse(JsonReading.WriteBody(body))!["body"]!.GetValue<string>();

        Assert.Equal(body, sent);
    }

    private static T Unwrap<T>(Result<T, string> result) => Assert.IsType<Result<T, string>.Ok>(result).Value;
}
