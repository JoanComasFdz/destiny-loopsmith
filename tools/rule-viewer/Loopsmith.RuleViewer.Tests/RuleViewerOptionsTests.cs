using Loopsmith.Core.Functional;

namespace Loopsmith.RuleViewer.Tests;

/// <summary>The tool's options: the rules folder, the output folder and the one rule to draw as a PNG.</summary>
public sealed class RuleViewerOptionsTests
{
    private static RuleViewerOptions ReadOk(params string[] args) =>
        RuleViewerOptions.ReadOptions(args, "/repo").Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    [Fact]
    public void Without_options_it_reads_rules_and_writes_the_page_to_out_rule_viewer()
    {
        var options = ReadOk();

        Assert.Equal(Path.Combine("/repo", "rules"), options.RulesDirectory);
        Assert.Equal(Path.Combine("/repo", "out", "rule-viewer"), options.OutDirectory);
        Assert.False(options.Rule.IsSome());
    }

    [Fact]
    public void Rule_names_the_rule_to_draw_and_out_its_folder()
    {
        var options = ReadOk("--rule", "keywords-arc-jolt-2", "--out", "shots");

        Assert.Equal(Optional.Some("keywords-arc-jolt-2"), options.Rule);
        Assert.Equal("shots", options.OutDirectory);
    }

    [Fact]
    public void Rule_without_a_name_is_an_error()
    {
        var read = RuleViewerOptions.ReadOptions(["--rule"], "/repo");

        var error = Assert.IsType<Result<RuleViewerOptions, string>.Error>(read);
        Assert.StartsWith("'--rule' needs a rule name.", error.Failure);
    }

    [Fact]
    public void An_unknown_option_is_an_error()
    {
        var read = RuleViewerOptions.ReadOptions(["--png", "x.png"], "/repo");

        var error = Assert.IsType<Result<RuleViewerOptions, string>.Error>(read);
        Assert.StartsWith("Unknown option '--png'.", error.Failure);
    }
}
