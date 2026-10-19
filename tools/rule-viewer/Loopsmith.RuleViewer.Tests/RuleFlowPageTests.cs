using System.Collections.Immutable;
using Loopsmith.RuleViewer.Core;
using Loopsmith.RuleViewer.Page;

namespace Loopsmith.RuleViewer.Tests;

/// <summary>The page a rule's PNG is taken of, in the theme <c>--theme</c> names.</summary>
public sealed class RuleFlowPageTests
{
    private static readonly PageAssets Assets = new(":root{}", "");

    [Fact]
    public void The_light_theme_draws_the_page_light()
    {
        var page = RuleFlowPage.RenderPage("keywords-arc-jolt-2", ImmutableArray<RuleFlow>.Empty, Assets, FlowTheme.Light);

        Assert.Contains("<html lang=\"en\" data-theme=\"light\">", page);
    }

    [Fact]
    public void The_dark_theme_draws_the_page_dark()
    {
        var page = RuleFlowPage.RenderPage("keywords-arc-jolt-2", ImmutableArray<RuleFlow>.Empty, Assets, FlowTheme.Dark);

        Assert.Contains("<html lang=\"en\" data-theme=\"dark\">", page);
    }
}
