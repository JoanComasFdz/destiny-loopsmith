using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.Phrasing;
using Loopsmith.Core.Simulation;
using Loopsmith.Core.SourceFetching;
using Loopsmith.Core.Tests.Support;
using Loopsmith.Core.TraceRendering;
using static VerifyXunit.Verifier;

namespace Loopsmith.Core.Tests.Golden;

/// <summary>
/// The example loops of builds/skip-grenade-hunter/loops/ (designed with <c>loopsmith play --save</c>): they import,
/// export back byte for byte, and their <c>loop</c> report and <c>compare</c> table are snapshots.
/// </summary>
public class ExampleLoopsGoldenTests
{
    private const string SkipGrenadesPath = "builds/skip-grenade-hunter/loops/infinite-skip-grenades.loop.yaml";
    private const string MeleeFirstPath = "builds/skip-grenade-hunter/loops/melee-first.loop.yaml";
    private const string HelicopterPath = "builds/skip-grenade-hunter-ascension/loops/helicopter-skip-grenades.loop.yaml";
    private const string BuildPath = "builds/skip-grenade-hunter/build.yaml";

    private static readonly RuleCatalog Catalog = LoadCatalog();

    private static RuleCatalog LoadCatalog() =>
        FileSourceFetching.ReadRuleFiles(RepoFiles.ToPath("rules"))
            .Bind(CatalogLoading.ParseCatalog)
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    private static SourceText ReadRepoFile(string path) => new(path, File.ReadAllText(RepoFiles.ToPath(path)));

    private static DesignSession ImportLoop(string path) =>
        LoopDesigning.ImportLoop(Catalog, ReadRepoFile(path)).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    private static LoopReport AnalyzeLoop(string path) =>
        LoopDesigning.AnalyzeDesign(ImportLoop(path));

    [Theory]
    [InlineData(SkipGrenadesPath)]
    [InlineData(MeleeFirstPath)]
    [InlineData(HelicopterPath)]
    public void Import_then_export_gives_back_the_file_byte_for_byte(string path)
    {
        var file = ReadRepoFile(path);

        var exported = LoopDesigning.ExportLoop(ImportLoop(path));

        Assert.Equal(file.Text, exported);
    }

    [Theory]
    [InlineData(SkipGrenadesPath)]
    [InlineData(MeleeFirstPath)]
    [InlineData(HelicopterPath)]
    public void Example_loops_have_a_description_and_a_note_on_every_step(string path)
    {
        var design = ImportLoop(path).Design;

        Assert.True(design.Description.IsSome());
        Assert.All(design.Steps, step => Assert.True(step.Note.IsSome()));
    }

    [Fact]
    public void The_creators_loop_repeats_and_wastes_tempest_strikes_bolt_charge()
    {
        var report = AnalyzeLoop(SkipGrenadesPath);

        Assert.Equal(new LoopVerdict.Repeats(Optional.None<BlockedStep>()), report.Verdict);
        var wasted = report.Steps.SelectMany(step => step.Wasted).ToImmutableArray();   // jolted kills: Tempest Strike's Bolt Charge doesn't stack with Dielectric's
        Assert.NotEmpty(wasted);
        Assert.All(wasted, mention => Assert.Equal(("tempest-strike", "Dielectric"), (mention.Source.Source.Value, mention.PartnerName)));
    }

    [Fact]
    public void The_ascension_variant_repeats_and_its_air_move_jolts_and_amplifies()
    {
        var session = ImportLoop(HelicopterPath);
        var report = LoopDesigning.AnalyzeDesign(session);

        Assert.IsType<LoopVerdict.Repeats>(report.Verdict);
        var airMove = session.Resolutions[1];   // after the new pack
        Assert.Equal(new PlayerAction.UseClassAbility(Airborne: true), airMove.Action);
        Assert.Contains(airMove.Fired, rule => rule.Source.Value == "ascension");
        Assert.Contains(airMove.Fired, rule => rule.Source.Value == "gamblers-dodge");   // the dodge's effects fire too
        Assert.Contains(airMove.State.Buffs, buff => buff.Status.Value == "amplified");
        Assert.Contains(airMove.State.Target.Debuffs, debuff => debuff.Value == "jolt");
    }

    [Fact]
    public void Slice_is_never_at_max_until_the_player_declares_it()
    {
        var report = AnalyzeLoop(MeleeFirstPath);   // one dodge per pass, no Strand weapon, no max:slice

        var resolutions = report.FirstPass.Resolutions.AddRange(report.RepeatingPass.Resolutions);
        Assert.DoesNotContain(resolutions.SelectMany(r => r.Fired), rule => rule.Trigger is GameEvent.StacksMaxed { Status.Value: "slice" });
        Assert.All(resolutions, resolution => Assert.False(resolution.State.Buffs.Any(b => b.Status.Value == "slice" && b.AtMax)));
    }

    [Fact]
    public void Melee_first_repeats_too_energy_never_breaks_a_loop()
    {
        var report = AnalyzeLoop(MeleeFirstPath);

        Assert.IsType<LoopVerdict.Repeats>(report.Verdict);
    }

    [Fact]
    public void A_designed_loop_exports_and_imports_back_to_the_same_session()
    {
        var started = LoopDesigning.StartDesign(Catalog, ReadRepoFile(BuildPath), "Round trip")
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
        var designed = LoopDesigning.AppendStep(started, new PlayerAction.UseClassAbility(), Optional.Some("dodge: \"arm\" # everything"));
        designed = LoopDesigning.AppendStep(designed, new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Kill, TargetCount.One), Optional.None<string>());
        designed = LoopDesigning.AppendStep(designed, new PlayerAction.Declare(new StateDeclaration.ReachMax(StatusId.From("bolt-charge"))), Optional.None<string>());
        designed = LoopDesigning.RenameDesign(designed, "Round trip: test", Optional.Some("Joan"), Optional.Some("Line one\n  indented line two\n"));

        var imported = LoopDesigning.ImportLoop(Catalog, new SourceText("x.loop.yaml", LoopDesigning.ExportLoop(designed)))
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

        Assert.Equal(designed.Design.Name, imported.Design.Name);
        Assert.Equal(designed.Design.Author, imported.Design.Author);
        Assert.Equal(designed.Design.Description, imported.Design.Description);
        Assert.Equal(designed.Design.Catalog, imported.Design.Catalog);
        Assert.Equal(designed.Design.Build.Text, imported.Design.Build.Text);
        Assert.Equal<LoopStep>(designed.Design.Steps, imported.Design.Steps);
        Assert.Equal(
            designed.Resolutions.Select(r => r.Fired.Length),
            imported.Resolutions.Select(r => r.Fired.Length));
        Assert.Equal(LoopDesigning.ExportLoop(designed), LoopDesigning.ExportLoop(imported));
    }

    [Fact]
    public void A_loop_designed_against_another_catalog_imports_with_an_info_note()
    {
        var file = ReadRepoFile(SkipGrenadesPath);
        var other = file with { Text = file.Text.Replace($"catalog: {Catalog.Version}", "catalog: authored-000000000000", StringComparison.Ordinal) };

        var session = LoopDesigning.ImportLoop(Catalog, other).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

        var issue = Assert.IsType<Optional<BuildIssue>.Some>(LoopDesigning.FindCatalogMismatch(session)).Value;
        Assert.Equal(Severity.Info, issue.Severity);
        Assert.Contains("authored-000000000000", issue.Message);
        Assert.Contains(issue, LoopDesigning.ListDesignIssues(session));
        Assert.False(LoopDesigning.FindCatalogMismatch(ImportLoop(SkipGrenadesPath)).IsSome());
    }

    [Fact]
    public void An_invalid_embedded_build_is_an_error_located_in_the_build()
    {
        var file = new SourceText("bad.loop.yaml", "loop: Bad\nsteps:\n  - do: class\nbuild: |\n  name: Bad\n  class: hunter\n  colour: red\n");

        var error = LoopDesigning.ImportLoop(Catalog, file).Match(_ => "", e => e.Failure);

        Assert.Contains("bad.loop.yaml#build:3: unknown key 'colour'", error);
    }

    [Fact]
    public void An_embedded_build_with_unknown_elements_fails_validation()
    {
        var file = ReadRepoFile(SkipGrenadesPath);
        var broken = file with { Text = file.Text.Replace("exoticArmor: shinobus-vow", "exoticArmor: no-such-exotic", StringComparison.Ordinal) };

        var error = LoopDesigning.ImportLoop(Catalog, broken).Match(_ => "", e => e.Failure);

        Assert.Contains("Unknown exotic armor 'no-such-exotic'", error);
    }

    // ── snapshots (review the diff when rules or the example loops change) ──────

    [Fact]
    public Task Loop_report_infinite_skip_grenades() => VerifyLoopReport(SkipGrenadesPath);

    [Fact]
    public Task Loop_report_melee_first() => VerifyLoopReport(MeleeFirstPath);

    [Fact]
    public Task Compare_example_loops()
    {
        var comparison = LoopDesigning.CompareLoops(AnalyzeLoop(SkipGrenadesPath), AnalyzeLoop(MeleeFirstPath));
        var text = LoopDesigning.RenderLoopComparison(comparison).ToPlainText();
        return Verify(text);
    }

    private static Task VerifyLoopReport(string path)
    {
        var session = ImportLoop(path);
        var report = LoopDesigning.AnalyzeDesign(session);
        var lines = LoopDesigning.RenderLoopReport(report)
            .Add(StyledText.ToLine(0, "".ToSpan()))
            .AddRange(LoopReportRendering.RenderLoopDesign(session.Design));
        return Verify(lines.ToPlainText());
    }
}
