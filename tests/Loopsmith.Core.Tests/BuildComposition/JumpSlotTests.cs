using Loopsmith.Core.BuildComposition;
using Loopsmith.Core.BuildParsing;
using Loopsmith.Core.Domain;
using Loopsmith.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.StepPlaying;
using Loopsmith.Core.SourceFetching;
using Loopsmith.Core.Tests.Support;

namespace Loopsmith.Core.Tests.BuildComposition;

/// <summary>
/// The jump slot (docs/rule-format.md, "Build check"): it takes only <c>kind: jump</c> elements of the build's class and
/// subclass; a jump is inert (movement has no part in a loop), so it is equipped without a warning and the engine never
/// plays it.
/// </summary>
public sealed class JumpSlotTests
{
    private const string BuildPath = "build.yaml";

    /// <summary>An Arcstrider with its abilities, an aspect and a fragment: something for a jump to set off, were it played.</summary>
    private const string ArcstriderText = """
        name: Arcstrider
        class: hunter
        subclass: arc
        super: arc-staff
        grenade: skip-grenade
        melee: combination-blow
        classAbility: gamblers-dodge
        aspects: [tempest-strike]
        fragments: [spark-of-shock]
        """;

    private static readonly RuleCatalog Catalog =
        FileSourceFetching.ReadRuleFiles(RepoFiles.ToPath("rules"))
            .Bind(CatalogLoading.ParseCatalog)
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    [Theory]
    [InlineData("high-jump", "High Jump", 95544330u)]
    [InlineData("strafe-jump", "Strafe Jump", 95544331u)]
    [InlineData("triple-jump", "Triple Jump", 95544328u)]
    [InlineData("hunter-blink", "Blink", 95544329u)]
    public void The_Arcstriders_jumps_are_inert_elements_of_the_rules(string id, string name, uint hash)
    {
        var jump = Catalog.Elements[ElementId.From(id)];

        Assert.Equal((name, ElementKind.Jump, Affinity.Arc), (jump.Name, jump.Kind, jump.Affinity));
        Assert.Equal([ItemHash.From(hash)], jump.Hashes);
        Assert.Equal(Optional.Some("A jump: movement has no part in a loop."), jump.Inert);
        Assert.Empty(jump.Rules);
        Assert.Empty(jump.Passives);
        Assert.False(jump.Ability.IsSome());
    }

    [Fact]
    public void A_jump_is_equipped_without_a_warning_and_sets_nothing_off()
    {
        var without = Validate(ArcstriderText);
        var with = Validate($"{ArcstriderText}\njump: triple-jump\n");

        var built = Assert.IsType<Result<ValidatedBuild, string>.Ok>(with).Value;
        var plain = Assert.IsType<Result<ValidatedBuild, string>.Ok>(without).Value;
        var initial = ActionResolution.CreateInitialState();

        Assert.Contains(built.Equipped, equipped => equipped.Element.Id == ElementId.From("triple-jump"));
        Assert.Equal(plain.Issues, built.Issues);
        Assert.Equal(ActionResolution.ListAvailableActions(plain, initial), ActionResolution.ListAvailableActions(built, initial));
    }

    [Fact]
    public void The_jump_slot_takes_only_a_jump_and_a_jump_goes_in_no_other_slot()
    {
        var grenadeAsJump = ValidateHunter("jump: skip-grenade");
        var jumpAsGrenade = ValidateHunter("grenade: triple-jump");

        Assert.Contains("'Skip Grenade' (Grenade) can't go in the jump slot.", Assert.IsType<Result<ValidatedBuild, string>.Error>(grenadeAsJump).Failure, StringComparison.Ordinal);
        Assert.Contains("'Triple Jump' (Jump) can't go in the grenade slot.", Assert.IsType<Result<ValidatedBuild, string>.Error>(jumpAsGrenade).Failure, StringComparison.Ordinal);
    }

    [Fact]
    public void A_jump_fits_only_its_class_and_subclass()
    {
        var onTitan = Validate("name: Striker\nclass: titan\nsubclass: arc\njump: triple-jump\n");
        var onGunslinger = Validate("name: Gunslinger\nclass: hunter\nsubclass: solar\njump: triple-jump\n");
        var onPrismatic = Validate("name: Prismatic\nclass: hunter\nsubclass: prismatic\njump: triple-jump\n");

        Assert.Contains("'Triple Jump' belongs to another class, not Titan.", Assert.IsType<Result<ValidatedBuild, string>.Error>(onTitan).Failure, StringComparison.Ordinal);
        Assert.Contains("'Triple Jump' (Arc) does not fit the Solar subclass.", Assert.IsType<Result<ValidatedBuild, string>.Error>(onGunslinger).Failure, StringComparison.Ordinal);
        Assert.IsType<Result<ValidatedBuild, string>.Ok>(onPrismatic);
    }

    [Fact]
    public void An_unknown_jump_is_part_of_the_build_and_says_nothing_under_validation()
    {
        var build = Assert.IsType<Result<ValidatedBuild, string>.Ok>(ValidateHunter("jump: \"?\"")).Value;

        Assert.Empty(build.Issues);
    }

    private static Result<ValidatedBuild, string> ValidateHunter(string line) =>
        Validate($"name: Hunter\nclass: hunter\nsubclass: arc\n{line}\n");

    private static Result<ValidatedBuild, string> Validate(string text) =>
        BuildFileParsing.ParseBuildFile(new SourceText(BuildPath, text), Catalog.Manifest)
            .Bind(build => BuildValidation.ValidateBuild(build, Catalog));
}
