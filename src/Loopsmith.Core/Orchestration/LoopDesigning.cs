using System.Collections.Immutable;
using Loopsmith.Core.BuildComposition;
using Loopsmith.Core.BuildExplanation;
using Loopsmith.Core.BuildParsing;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.LoadoutImporting;
using Loopsmith.Core.ManifestJoining;
using Loopsmith.Core.LoopFiles;
using Loopsmith.Core.Phrasing;
using Loopsmith.Core.ReportComparison;
using Loopsmith.Core.RuleParsing;
using Loopsmith.Core.Simulation;
using Loopsmith.Core.TraceRendering;

namespace Loopsmith.Core.Orchestration;

public enum TriggerGroup { Ability, Weapon, Pickup, Declare }

/// <summary>
/// A step the designer can pick next — every option listed can happen (abilities are always available, ADRs D5;
/// a declaration is offered only when it holds, ADRs D3). <see cref="IsNew"/> marks what the last step unlocked (a
/// pickup that just landed, a buff that can now be declared at max). Abilities and weapons are listed
/// against one enemy; <see cref="LoopDesigning.SetTargetCount"/> aims them at more.
/// </summary>
public sealed record TriggerOption(
    PlayerAction Action,
    string Token,
    string Label,
    TriggerGroup Group,
    bool IsNew);

/// <summary>
/// A loop being designed: the validated build, the design so far, one resolution per step (same order as
/// <c>Design.Steps</c>) and the state after the last step. Immutable — every edit returns a new session,
/// so undo is "keep the previous session".
/// </summary>
public sealed record DesignSession(
    ValidatedBuild Build,
    LoopDesign Design,
    GameState Initial,
    ImmutableArray<Resolution> Resolutions,
    GameState Current);

/// <summary>A catalog with what Bungie's full manifest said added; <see cref="Note"/> says when it couldn't be read.</summary>
public sealed record JoinedCatalog(RuleCatalog Catalog, Optional<string> Note);

/// <summary>Pure API the hosts (CLI, web) use to design, export, import, analyse and compare loops.</summary>
public static class LoopDesigning
{
    public static Result<DesignSession, string> StartDesign(RuleCatalog catalog, SourceText buildFile, string name) =>
        BuildFileParsing.ParseBuildFile(buildFile)
            .Bind(build => BuildValidation.ValidateBuild(build, catalog))
            .Map(validated => CreateSession(validated, new LoopDesign(
                name,
                Optional.None<string>(),
                Optional.None<string>(),
                Optional.Some(catalog.Version),
                buildFile,
                [])));

    /// <summary>The comment a build from a DIM link starts with: where it came from and what <c>leftOut</c> means.</summary>
    public const string DimBuildNote =
        "Composed by Loopsmith from the DIM loadout at source. leftOut: what the loadout has that the rule catalog\n"
        + "doesn't know yet, by manifest hash.";

    /// <summary>Pasted text → the DIM link it is: a dim.gg share (DIM's Share button) or a link that carries its loadout.</summary>
    public static Result<DimLink, string> ReadDimLink(string text) =>
        DimLinkReading.ReadDimLink(text);

    /// <summary>Where a host asks DIM for a dim.gg share's loadout (an HTTP GET with the app's DIM API key as <c>X-API-Key</c>).</summary>
    public static string ToDimShareRequestUrl(DimLink.Shared share) =>
        DimLinkReading.ToShareRequestUrl(share.ShareId);

    /// <summary>
    /// A new design for a DIM loadout: <paramref name="loadoutJson"/> is the link's own loadout, or DIM's answer for a
    /// dim.gg share. What the catalog recognises by hash becomes the build (the rest is its <c>leftOut</c>); the build is
    /// written as a build file, so the loop embeds it — and exports, shares and replays it — like any other.
    /// </summary>
    public static Result<DesignSession, string> StartDimDesign(RuleCatalog catalog, DimLink link, string loadoutJson) =>
        DimLoadoutMapping.MapLoadout(catalog, ReadLink(link), loadoutJson)
            .Map(build => new SourceText("dim-loadout.build.yaml", BuildFileWriting.WriteBuildFile(build, [DimBuildNote])))
            .Bind(file => StartDesign(catalog, file, "New loop"))
            .Map(session => RenameDesign(session, $"{session.Build.Build.Name} loop", session.Design.Author, session.Design.Description));

    /// <summary>A new design for a saved DIM share (<c>{ "link": …, "loadout": … }</c>): the same as pasting its link.</summary>
    public static Result<DesignSession, string> StartSavedDimDesign(RuleCatalog catalog, SourceText savedShare) =>
        DimLinkReading.ReadSavedLink(savedShare.Text)
            .MapError(error => $"{savedShare.Path}: {error}")
            .Bind(link => StartDimDesign(catalog, link, savedShare.Text));

    // ── Bungie's full manifest, for what the excerpt lacks (ManifestJoining) ──────────

    /// <summary>The hashes a DIM loadout wears that the catalog's manifest excerpt lacks (what the full manifest is read for).</summary>
    public static Result<ImmutableArray<ItemHash>, string> ListUnknownLoadoutHashes(RuleCatalog catalog, string loadoutJson) =>
        DimLoadoutMapping.ListLoadoutHashes(loadoutJson).Map(hashes => ManifestJoin.ListUnknown(catalog.Manifest, hashes));

    /// <summary>The hashes an open design's build names that the excerpt lacks (its weapons, picks, what it left out).</summary>
    public static ImmutableArray<ItemHash> ListUnknownHashes(DesignSession session) =>
        ManifestJoin.ListUnknownBuildHashes(session.Build.Catalog.Manifest, session.Build.Build);

    /// <summary>Of <paramref name="hashes"/>, those the host hasn't read the manifest for yet (<paramref name="asked"/>: found, missing or failed).</summary>
    public static ImmutableArray<ItemHash> ListUnaskedHashes(ImmutableHashSet<ItemHash> asked, IEnumerable<ItemHash> hashes) =>
        ManifestJoin.ListUnasked(asked, hashes);

    /// <summary>What the host says while it reads the full manifest for <paramref name="count"/> hashes.</summary>
    public static string DescribeManifestReading(int count) =>
        $"Reading Bungie's manifest for {count} item{(count == 1 ? "" : "s")} Loopsmith doesn't know yet…";

    /// <summary>The manifest's index on bungie.net (the paths of its current version's components).</summary>
    public const string ManifestIndexUrl = ManifestTransport.IndexUrl;

    /// <summary>Each manifest component's URL on bungie.net, by component name: the index's, else the pinned content id's.</summary>
    public static ImmutableDictionary<string, string> ChooseManifestUrls(Optional<string> indexJson, string pinnedContentId) =>
        ManifestTransport.ChooseUrls(indexJson, pinnedContentId);

    /// <summary>A join of what these hashes need from the full manifest: its first question (or, for none, the excerpt).</summary>
    public static ManifestJoinStep StartManifestJoin(IEnumerable<ItemHash> hashes) =>
        ManifestJoin.StartJoin(hashes);

    /// <summary>What the host reads for a question, per component (an empty list: the whole component).</summary>
    public static ImmutableDictionary<string, ImmutableArray<ItemHash>> ToManifestRequest(ManifestQuery query) =>
        ManifestTransport.ToEntryRequest(query);

    /// <summary>What the host searches the item component for, for a question's plug categories.</summary>
    public static ManifestTextSearch ToManifestPlugSearch(ManifestQuery query) =>
        ManifestTransport.ToPlugSearch(query);

    /// <summary>The join with the host's answer to its question: its next step, or why the answer doesn't read.</summary>
    public static Result<ManifestJoinStep, string> ContinueManifestJoin(ManifestJoinStep.Ask asked, string entriesJson, string foundJson) =>
        ManifestTransport.ReadAnswer(asked.Query, entriesJson, foundJson)
            .Map(answer => ManifestJoin.ContinueJoin(asked.Progress, answer));

    /// <summary>The excerpt the join built, or why reading the manifest failed (or didn't finish).</summary>
    public static Result<ManifestExcerpt, string> ToJoinedExcerpt(Result<ManifestJoinStep, string> step) =>
        ManifestTransport.ToJoinedExcerpt(step);

    /// <summary>
    /// The catalog with what the full manifest said added to its excerpt (what it had stays); when the manifest
    /// couldn't be read, the catalog as it is and a note saying so.
    /// </summary>
    public static JoinedCatalog AddJoinedManifest(RuleCatalog catalog, Result<ManifestExcerpt, string> joined) =>
        joined.Match(
            ok => new JoinedCatalog(AddManifest(catalog, ok.Value), Optional.None<string>()),
            error => new JoinedCatalog(catalog, Optional.Some(DescribeManifestProblem(error.Failure))));

    /// <summary>
    /// The design with what the full manifest said of its build: validated again with it, its steps replayed; else why
    /// not (the manifest couldn't be read, or the build doesn't validate with it).
    /// </summary>
    public static Result<DesignSession, string> UseJoinedManifest(DesignSession session, Result<ManifestExcerpt, string> joined) =>
        joined
            .MapError(DescribeManifestProblem)
            .Bind(excerpt => UseCatalog(session, AddManifest(session.Build.Catalog, excerpt)));

    /// <summary>The catalog with a joined part of the full manifest added to its excerpt (what it had stays).</summary>
    private static RuleCatalog AddManifest(RuleCatalog catalog, ManifestExcerpt joined) =>
        catalog with { Manifest = catalog.Manifest.AddExcerpt(joined) };

    private static string DescribeManifestProblem(string problem) =>
        $"Couldn't read Bungie's manifest ({problem}). Items Loopsmith doesn't know show by hash.";

    /// <summary>The design with its build validated again against a catalog (one with more of the manifest), its steps replayed.</summary>
    private static Result<DesignSession, string> UseCatalog(DesignSession session, RuleCatalog catalog) =>
        BuildValidation.ValidateBuild(session.Build.Build, catalog)
            .Map(validated => CreateSession(validated, session.Design));

    /// <summary>True when a build's source is a DIM link (the designer calls it one).</summary>
    public static bool IsDimLink(string url) =>
        DimLinkReading.ReadDimLink(url) is Result<DimLink, string>.Ok;

    private static string ReadLink(DimLink link) =>
        link.Match(shared => shared.Link, inline => inline.Link);

    /// <summary>
    /// The design with a weapon's perk picked in one of its trait columns (or that column back to "?"): weapon
    /// <paramref name="weapon"/> and column <paramref name="column"/> are 0-based, in the build's order. Only that
    /// weapon's entry of the embedded build file changes; the build is validated again and the steps replayed with it.
    /// </summary>
    public static Result<DesignSession, string> PickWeaponPerk(DesignSession session, int weapon, int column, Optional<ItemHash> perk) =>
        EditWeapon(session, weapon, loadout => WeaponRolling.PickPerk(session.Build.Catalog, loadout, column, perk));

    /// <summary>
    /// The design with an exotic weapon's catalyst on or off (<paramref name="weapon"/> 0-based, in the build's order):
    /// only that weapon's entry of the embedded build file changes (<c>catalyst: true</c>); the steps replay with it.
    /// </summary>
    public static Result<DesignSession, string> SetCatalyst(DesignSession session, int weapon, bool on) =>
        EditWeapon(session, weapon, loadout => WeaponRolling.SetCatalyst(session.Build.Catalog, loadout, on));

    /// <summary>The build's armor sets: per set, the pieces worn (when the build says) and which of its perks are active.</summary>
    public static ImmutableArray<ArmorSetStatus> ListArmorSets(ValidatedBuild build) =>
        ArmorSetting.ListArmorSets(build.Catalog, build.Build);

    /// <summary>The build's set bonuses in none of the manifest excerpt's sets (shown on their own).</summary>
    public static ImmutableArray<ElementId> ListLooseSetBonuses(ValidatedBuild build) =>
        ArmorSetting.ListLooseBonuses(build.Catalog, build.Build);

    /// <summary>
    /// What an element sets off in the build — each trigger, then its outcomes and reasons — for an item's details; one
    /// the build doesn't equip (a catalyst that is off, a set perk not active) as one copy would.
    /// </summary>
    public static ImmutableArray<StyledLine> ExplainElement(ValidatedBuild build, BuildElement element) =>
        BuildExplaining.RenderElementExplanation(BuildExplaining.ExplainElement(build, element));

    /// <summary>True when the build equips the element (its slots, a weapon's perks, a catalyst that is on, its set bonuses).</summary>
    public static bool IsEquipped(ValidatedBuild build, ElementId id) =>
        build.Equipped.Any(equipped => equipped.Element.Id == id);

    /// <summary>The build's weapon number <paramref name="weapon"/> (0-based, in the build's order), if it has one.</summary>
    public static Optional<WeaponLoadout> FindWeapon(ValidatedBuild build, int weapon) =>
        FindWeapon(build.Build, weapon).Match(found => Optional.Some(found.Value), _ => Optional.None<WeaponLoadout>());

    /// <summary>That weapon's catalyst, when the manifest excerpt gives it one.</summary>
    public static Optional<WeaponPerkSlot.Catalyst> FindCatalyst(ValidatedBuild build, int weapon) =>
        FindWeapon(build, weapon).Bind(found => WeaponRolling.ListPerkSlots(build.Catalog, found)
            .OfType<WeaponPerkSlot.Catalyst>()
            .Select(Optional.Some)
            .FindFirstSome());

    /// <summary>One weapon of the embedded build changed: that entry rewritten, the build validated again, the steps replayed.</summary>
    private static Result<DesignSession, string> EditWeapon(DesignSession session, int weapon, Func<WeaponLoadout, Result<WeaponLoadout, string>> change) =>
        FindWeapon(session.Build.Build, weapon)
            .Bind(change)
            .Bind(changed => BuildFileEditing.ReplaceWeapon(session.Design.Build, weapon, changed))
            .Map(text => session.Design.Build with { Text = text })
            .Bind(file => BuildFileParsing.ParseBuildFile(file)
                .Bind(build => BuildValidation.ValidateBuild(build, session.Build.Catalog))
                .Map(validated => CreateSession(validated, session.Design with { Build = file })));

    /// <summary>A weapon's perks the way the build holds them, column by column (what the designer shows and the build check equips).</summary>
    public static ImmutableArray<WeaponPerkSlot> ListPerkSlots(ValidatedBuild build, WeaponLoadout weapon) =>
        WeaponRolling.ListPerkSlots(build.Catalog, weapon);

    private static Result<WeaponLoadout, string> FindWeapon(Build build, int weapon) =>
        weapon >= 0 && weapon < build.Weapons.Length
            ? new Result<WeaponLoadout, string>.Ok(build.Weapons[weapon])
            : new Result<WeaponLoadout, string>.Error($"The build has no weapon {weapon + 1}.");

    public static DesignSession CreateSession(ValidatedBuild build, LoopDesign design)
    {
        var initial = ActionResolution.CreateInitialState();
        var actions = design.Steps.Select(step => step.Action).ToImmutableArray();
        var resolutions = ActionResolution.ResolveSequence(build, initial, actions);
        var current = resolutions.IsEmpty ? initial : resolutions[^1].State;
        return new DesignSession(build, design, initial, resolutions, current);
    }

    public static DesignSession AppendStep(DesignSession session, PlayerAction action, Optional<string> note)
    {
        var resolution = ActionResolution.ResolveAction(session.Build, session.Current, action);
        var design = session.Design with { Steps = session.Design.Steps.Add(new LoopStep(action, note)) };
        return session with { Design = design, Resolutions = session.Resolutions.Add(resolution), Current = resolution.State };
    }

    public static DesignSession RemoveLastStep(DesignSession session) =>
        TruncateSteps(session, session.Design.Steps.Length - 1);

    /// <summary>Keep the first <paramref name="keep"/> steps — branch from there with a different trigger.</summary>
    public static DesignSession TruncateSteps(DesignSession session, int keep)
    {
        var count = Math.Clamp(keep, 0, session.Design.Steps.Length);
        var resolutions = session.Resolutions.Take(count).ToImmutableArray();
        var design = session.Design with { Steps = session.Design.Steps.Take(count).ToImmutableArray() };
        var current = resolutions.IsEmpty ? session.Initial : resolutions[^1].State;
        return session with { Design = design, Resolutions = resolutions, Current = current };
    }

    /// <summary>
    /// The design without step <paramref name="index"/> (0-based): every later step replays without it (a step whose
    /// cause is gone shows blocked). Out of range: the design as it is.
    /// </summary>
    public static DesignSession RemoveStep(DesignSession session, int index) =>
        index < 0 || index >= session.Design.Steps.Length
            ? session
            : CreateSession(session.Build, session.Design with { Steps = session.Design.Steps.RemoveAt(index) });

    /// <summary>
    /// The design with step <paramref name="from"/> moved to place <paramref name="to"/> (0-based, its place after the
    /// move), its note with it: the steps replay in the new order. Out of range, or the same place: the design as it is.
    /// </summary>
    public static DesignSession MoveStep(DesignSession session, int from, int to) =>
        from < 0 || from >= session.Design.Steps.Length || to < 0 || to >= session.Design.Steps.Length || from == to
            ? session
            : CreateSession(session.Build, session.Design with { Steps = session.Design.Steps.RemoveAt(from).Insert(to, session.Design.Steps[from]) });

    public static DesignSession RenameDesign(DesignSession session, string name, Optional<string> author, Optional<string> description) =>
        session with { Design = session.Design with { Name = name, Author = author, Description = description } };

    public static DesignSession AnnotateStep(DesignSession session, int index, Optional<string> note) =>
        index < 0 || index >= session.Design.Steps.Length
            ? session
            : session with { Design = session.Design with { Steps = session.Design.Steps.SetItem(index, session.Design.Steps[index] with { Note = note }) } };

    /// <summary>
    /// Every step the build offers right now, grouped: every ability (always available — ADRs D5), every weapon, the
    /// pickups on the ground and the states you can declare (ADRs D3). <see cref="TriggerOption.IsNew"/> marks what the
    /// last step made available.
    /// </summary>
    public static ImmutableArray<TriggerOption> ListTriggerOptions(DesignSession session)
    {
        var build = session.Build;
        var glossary = build.Catalog.Glossary;
        var previous = session.Resolutions.Length switch
        {
            0 => [],
            1 => ActionResolution.ListAvailableActions(build, session.Initial),
            _ => session.Resolutions[^2].NowAvailable,
        };
        var candidates = ListCandidateActions(build, session.Current);
        return candidates
            .Select(candidate => new TriggerOption(
                candidate.Action,
                candidate.Action.ToActionToken(),
                glossary.DescribeAction(candidate.Action, build.Build),
                candidate.Group,
                !previous.Contains(candidate.Action) && !session.Resolutions.IsEmpty))
            .ToImmutableArray();
    }

    private static ImmutableArray<(PlayerAction Action, TriggerGroup Group)> ListCandidateActions(ValidatedBuild build, GameState state)
    {
        var hits = new[] { HitOutcome.Kill, HitOutcome.Damage };
        var abilities = new[] { OffensiveAbility.Grenade, OffensiveAbility.Melee }
            .SelectMany(kind => hits.Select(hit => ((PlayerAction)new PlayerAction.CastAbility(kind, hit, TargetCount.One), TriggerGroup.Ability)))
            .Concat(ActionResolution.ListClassAbilityActions(build).Select(use => (use, TriggerGroup.Ability)))
            .Concat(hits.Select(hit => ((PlayerAction)new PlayerAction.CastAbility(OffensiveAbility.Super, hit, TargetCount.One), TriggerGroup.Ability)));
        var weapons = build.Build.Weapons
            .SelectMany(weapon => hits.Select(hit => ((PlayerAction)new PlayerAction.FireWeapon(weapon.Slot, hit, TargetCount.One), TriggerGroup.Weapon)));
        var pickups = state.Pickups
            .Select(pickup => ((PlayerAction)new PlayerAction.CollectPickups(pickup), TriggerGroup.Pickup));
        var declarations = ActionResolution.ListDeclarations(build, state)
            .Select(declaration => ((PlayerAction)new PlayerAction.Declare(declaration), TriggerGroup.Declare));
        return [.. abilities, .. weapons, .. pickups, .. declarations];
    }

    /// <summary>How many enemies an ability or weapon action hits (or kills); none for the class ability, pickups and declarations.</summary>
    public static Optional<TargetCount> ReadTargetCount(PlayerAction action) =>
        action switch
        {
            PlayerAction.CastAbility cast => Optional.Some(cast.Targets),
            PlayerAction.FireWeapon fire => Optional.Some(fire.Targets),
            _ => Optional.None<TargetCount>(),
        };

    /// <summary>
    /// The same ability or weapon action against <paramref name="targets"/> enemies ("kill 3 with the grenade"); the
    /// class ability, pickups and declarations have no targets and come back unchanged. Hosts validate a typed count with
    /// <c>TargetCount.TryFrom</c> (1..20).
    /// </summary>
    public static PlayerAction SetTargetCount(PlayerAction action, TargetCount targets) =>
        action switch
        {
            PlayerAction.CastAbility cast => cast with { Targets = targets },
            PlayerAction.FireWeapon fire => fire with { Targets = targets },
            _ => action,
        };

    // ── export, import, analyse, compare (LoopFiles, Simulation.LoopRunning, ReportComparison) ──

    /// <summary>The design as a <c>.loop.yaml</c> file (docs/loop-format.md).</summary>
    public static string ExportLoop(DesignSession session) =>
        LoopFileWriting.WriteLoopFile(session.Design);

    /// <summary>
    /// Parse a <c>.loop.yaml</c>, validate its embedded build against the catalog, replay its steps. A loop designed
    /// against another catalog still imports (see <see cref="ListDesignIssues"/>); an invalid embedded build or an
    /// unknown action token is an error.
    /// </summary>
    public static Result<DesignSession, string> ImportLoop(RuleCatalog catalog, SourceText loopFile) =>
        LoopFileParsing.ParseLoopFile(loopFile)
            .Bind(design => BuildFileParsing.ParseBuildFile(design.Build)
                .Bind(build => BuildValidation.ValidateBuild(build, catalog))
                .Map(validated => CreateSession(validated, design)));

    /// <summary>
    /// The design analysed by its order of triggers: played from a fresh spawn, then again from where it ended until
    /// it repeats — what each step needs and from which step, what it sets off, what is wasted (ADRs D2).
    /// </summary>
    public static LoopReport AnalyzeDesign(DesignSession session) =>
        LoopRunning.RunLoop(session.Build, session.Design);

    public static LoopComparison CompareLoops(LoopReport left, LoopReport right) =>
        LoopComparing.CompareLoops(left, right);

    /// <summary>
    /// The build's own validation issues (warnings, info) plus, as Info, a loop designed against a catalog other
    /// than the one it is replayed with.
    /// </summary>
    public static ImmutableArray<BuildIssue> ListDesignIssues(DesignSession session) =>
        [.. session.Build.Issues, .. FindCatalogMismatch(session).Match(issue => [issue.Value], _ => ImmutableArray<BuildIssue>.Empty)];

    /// <summary>Info when the loop was designed against another catalog version than the one replaying it.</summary>
    public static Optional<BuildIssue> FindCatalogMismatch(DesignSession session)
    {
        var current = session.Build.Catalog.Version;
        return session.Design.Catalog.Bind(designed => designed == current
            ? Optional.None<BuildIssue>()
            : Optional.Some(new BuildIssue(Severity.Info, $"Loop designed against catalog {designed}; replaying with {current}.")));
    }

    /// <summary>What one step fired, worded like the CLI's trace, without its "#n action" header (the host draws its own).</summary>
    public static ImmutableArray<StyledLine> RenderStepOutcomes(DesignSession session, Resolution resolution, TraceOptions options) =>
        [.. TraceRenderer.RenderResolution(session.Build, resolution, options).Skip(1)];

    /// <summary>The static view of a build: every trigger and what it sets off, as a build note or an aligned tree.</summary>
    public static ImmutableArray<StyledLine> ExplainBuild(ValidatedBuild build, ExplanationStyle style) =>
        BuildExplaining.RenderExplanation(BuildExplaining.ExplainBuild(build), style);

    /// <summary>The report as styled lines: the order, the verdict, each step's needs, what it sets off, what is wasted.</summary>
    public static ImmutableArray<StyledLine> RenderLoopReport(LoopReport report) =>
        LoopReportRendering.RenderLoopReport(report);

    /// <summary>The comparison as styled lines: both orders and their verdicts, then trigger by trigger.</summary>
    public static ImmutableArray<StyledLine> RenderLoopComparison(LoopComparison comparison) =>
        ComparisonRendering.RenderComparison(comparison);
}

/// <summary>Parse the rule catalog once (the web host reads the rule files from embedded resources).</summary>
public static class CatalogLoading
{
    public static Result<RuleCatalog, string> ParseCatalog(ImmutableArray<SourceText> ruleFiles) =>
        RuleCatalogParsing.ParseCatalog(ruleFiles);
}
