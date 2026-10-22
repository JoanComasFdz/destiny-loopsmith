using System.Collections.Immutable;
using Loopsmith.Core.BuildComposition;
using Loopsmith.Core.BuildExplanation;
using Loopsmith.Core.BuildParsing;
using Loopsmith.Core.Causality;
using Loopsmith.Core.Domain;
using Loopsmith.Functional;
using Loopsmith.Core.LoopAnalyzing;
using Loopsmith.Core.ManifestJoining;
using Loopsmith.Core.LoopFiles;
using Loopsmith.Core.Phrasing;
using Loopsmith.Core.RuleParsing;
using Loopsmith.Core.StepPlaying;
using Loopsmith.Core.StatLoading;
using Loopsmith.Core.TraceRendering;

namespace Loopsmith.Core.Orchestration;

public enum TriggerGroup { Ability, Weapon, AmmoBrick, Pickup, Declare }

/// <summary>
/// A step the designer can pick next — every option listed can happen (abilities are always available, ADRs D5;
/// a declaration is offered only when it holds, ADRs D3). <see cref="IsNew"/> marks what the last step unlocked (a
/// pickup that just landed, a buff that can now be declared at max). Abilities and weapons are listed
/// against one enemy; <see cref="LoopDesigning.SetTargetCount"/> aims them at more. <see cref="IsEnabled"/>: false for a
/// step that always shows but that no rule of the build reacts to (a weapon's Ready step without Mechaneer's
/// Tricksleeves), so it is there to see but sets nothing off.
/// </summary>
public sealed record TriggerOption(
    PlayerAction Action,
    string Token,
    string Label,
    TriggerGroup Group,
    bool IsNew,
    bool IsEnabled = true);

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

/// <summary>
/// The design after the player's Bungie login (its weapons holding their copies' perks when the copies are theirs, its
/// stats set when its armor is), and what to tell them.
/// </summary>
public sealed record BungieLogin(DesignSession Session, string Message);

/// <summary>A catalog with what Bungie's full manifest said added; <see cref="Note"/> says when it couldn't be read.</summary>
public sealed record JoinedCatalog(RuleCatalog Catalog, Optional<string> Note);

/// <summary>Pure API the host (the web designer) uses to design, export, import and analyse loops.</summary>
public static class LoopDesigning
{
    public static Result<DesignSession, string> StartDesign(RuleCatalog catalog, SourceText buildFile, string name) =>
        BuildFileParsing.ParseBuildFile(buildFile, catalog.Manifest)
            .Bind(build => BuildValidation.ValidateBuild(build, catalog))
            .Map(validated => CreateSession(validated, new LoopDesign(
                name,
                Optional.None<string>(),
                Optional.None<string>(),
                Optional.Some(catalog.Version),
                buildFile,
                [])));

    /// <summary>The name of the build a loop started from scratch has (its loop: "Arc Hunter loop").</summary>
    public const string ScratchBuildName = "Arc Hunter";

    /// <summary>The comment a build started from scratch begins with.</summary>
    public const string ScratchBuildNote =
        "Started from scratch in Loopsmith's designer: an Arc Hunter with nothing equipped yet. An ability left out is an\n"
        + "empty slot.";

    /// <summary>
    /// A new design for a build started from scratch: an Arc Hunter (Arcstrider) with nothing equipped — no super,
    /// abilities, aspects or fragments, no weapons, no armor, no stats. The player fills it in the designer (the subclass
    /// editor; their own weapons and armor after a Bungie login). It is written as a build file, so the loop embeds it —
    /// and exports, shares and replays it — like any other.
    /// </summary>
    public static Result<DesignSession, string> StartScratchDesign(RuleCatalog catalog)
    {
        var build = CreateScratchBuild();
        var file = new SourceText("from-scratch.build.yaml", BuildFileWriting.WriteBuildFile(build, [ScratchBuildNote]));
        return StartDesign(catalog, file, $"{ScratchBuildName} loop");
    }

    private static Build CreateScratchBuild()
    {
        var empty = new AbilityPlug.Empty();
        var noStat = Optional.None<StatValue>();
        return new Build(
            ScratchBuildName,
            Optional.None<string>(),
            Optional.None<string>(),
            Optional.None<CatalogVersion>(),
            GuardianClass.Hunter,
            Subclass.Arc,
            new AbilityLoadout(empty, empty, empty, empty, empty),
            [],
            [],
            Optional.None<ElementId>(),
            [],
            [],
            [],
            [],
            new StatLine(noStat, noStat, noStat, noStat, noStat, noStat),
            [],
            [],
            [],
            Optional.None<ArtifactLoadout>());
    }

    /// <summary>
    /// True when the build equips anything: an ability (named or "?"), an aspect, a fragment, a weapon, an armor piece, a
    /// mod, an artifact or one of its perks, or something the rules don't know yet. The finisher, a new pack and the
    /// declared states need nothing equipped, so a build with only a fragment can set things off. A build started from
    /// scratch equips nothing until the subclass editor, a weapon or an armor piece gives it something, and the designer
    /// offers no trigger until then.
    /// </summary>
    public static bool HasTriggerSource(ValidatedBuild build) =>
        IsEquippingAnything(build.Build);

    private static bool IsEquippingAnything(Build build) =>
        build.Abilities.ListPlugs().Append(build.Abilities.Jump).Any(plug => !plug.IsEmpty())
        || !build.Aspects.IsEmpty
        || !build.Fragments.IsEmpty
        || build.ExoticArmor.IsSome()
        || !build.ArmorSetBonuses.IsEmpty
        || !build.ArmorMods.IsEmpty
        || !build.ArtifactPerks.IsEmpty
        || !build.Weapons.IsEmpty
        || !build.LeftOut.IsEmpty
        || !build.ArmorCopies.IsEmpty
        || !build.ArmorModPlugs.IsEmpty
        || build.Artifact.IsSome();

    // ── Bungie's full manifest, for what the excerpt lacks (ManifestJoining) ──────────

    /// <summary>The hashes an open design's build names that the excerpt lacks (its weapons, picks, what it left out).</summary>
    public static ImmutableArray<ItemHash> ListUnknownHashes(DesignSession session) =>
        ManifestJoin.ListUnknownBuildHashes(session.Build.Catalog.Manifest, session.Build.Build);

    /// <summary>
    /// The hashes of the weapons a build file gives by hash that the excerpt lacks: what the host reads the full manifest
    /// for before it parses the file (a weapon's hash the manifest doesn't know is refused). None when the file doesn't
    /// read: importing it says why.
    /// </summary>
    public static ImmutableArray<ItemHash> ListUnknownBuildFileHashes(RuleCatalog catalog, SourceText buildFile) =>
        BuildFileParsing.ListWeaponHashes(buildFile)
            .Match(ok => ManifestJoin.ListUnknown(catalog.Manifest, ok.Value), _ => []);

    /// <summary>The same for the build a <c>.loop.yaml</c> embeds.</summary>
    public static ImmutableArray<ItemHash> ListUnknownLoopFileHashes(RuleCatalog catalog, SourceText loopFile) =>
        LoopFileParsing.ParseLoopFile(loopFile)
            .Match(ok => ListUnknownBuildFileHashes(catalog, ok.Value.Build), _ => []);

    /// <summary>Of <paramref name="hashes"/>, those the host hasn't read the manifest for yet (<paramref name="asked"/>: found, missing or failed).</summary>
    public static ImmutableArray<ItemHash> ListUnaskedHashes(ImmutableHashSet<ItemHash> asked, IEnumerable<ItemHash> hashes) =>
        ManifestJoin.ListUnasked(asked, hashes);

    /// <summary>What the host says while it reads the full manifest for <paramref name="count"/> hashes.</summary>
    public static string DescribeManifestReading(int count) =>
        $"Reading Bungie's manifest for {count} item{(count == 1 ? "" : "s")} Loopsmith doesn't know yet…";

    /// <summary>The manifest's index on bungie.net (the paths of its current version's components).</summary>
    public const string ManifestIndexUrl = ManifestTransport.IndexUrl;

    /// <summary>
    /// Each manifest component's URL on bungie.net, by component name (every component the join and the listing read: the
    /// web app loads them all when it starts and keeps them for the tab): the index's, else the pinned content id's.
    /// </summary>
    public static ImmutableDictionary<string, string> ChooseManifestUrls(Optional<string> indexJson, string pinnedContentId) =>
        ManifestTransport.ChooseUrls(indexJson, pinnedContentId);

    // ── The manifest's copy saved in the browser between visits (ADRs D14, D25) ─────────────────────────────

    /// <summary>The copy of the manifest the browser saved (<c>{ "urls": …, "bytes": n }</c>), when there's one with every component.</summary>
    public static Optional<SavedManifest> ReadSavedManifest(Optional<string> savedJson) =>
        ManifestSaving.ReadSavedManifest(savedJson);

    /// <summary>The saved copy when the manifest index names its version (or doesn't answer), else a download of the current (or pinned) version.</summary>
    public static ManifestSource ChooseManifestSource(Optional<string> indexJson, string pinnedContentId, Optional<SavedManifest> saved) =>
        ManifestSaving.ChooseSource(indexJson, pinnedContentId, saved);

    /// <summary>The key a listing is saved under: the manifest version it was read from and the code that made it.</summary>
    public static string ToListingKey(ImmutableDictionary<string, string> urls, string codeVersion) =>
        ManifestSaving.ToListingKey(urls, codeVersion);

    /// <summary>The listing of every weapon and armor piece as the browser saves it.</summary>
    public static string ToSavedListing(ItemListing listing) =>
        ManifestSaving.ToListingText(listing);

    /// <summary>The listing the browser saved under the key asked for, when there's one and it reads.</summary>
    public static Optional<ItemListing> ReadSavedListing(Optional<string> savedText) =>
        savedText.Bind(text => ManifestSaving.ReadListingText(text).Match(
            ok => Optional.Some(ok.Value),
            _ => Optional.None<ItemListing>()));

    // ── Every weapon and armor piece in the game (the pickers' "Every item", docs/loop-format.md) ──────────────

    /// <summary>How many questions a listing asks at most (the startup screen's "step 2 of 4").</summary>
    public const int ListingRounds = ManifestListing.Rounds;

    /// <summary>A listing of every weapon and armor piece a build can wear: its first question to the host.</summary>
    public static ListingStep StartListing() =>
        ManifestListing.StartListing();

    /// <summary>Which of its <see cref="ListingRounds"/> questions a listing is asking (1 to 4).</summary>
    public static int ToListingRound(ListingStep.Ask asked) =>
        ManifestListing.ToRound(asked.Progress);

    /// <summary>The listing with the host's answers to its question (its entries, its parts): its next step, or why an answer doesn't read.</summary>
    public static Result<ListingStep, string> ContinueListing(ListingStep.Ask asked, string entriesJson, string partsJson) =>
        ManifestListing.ContinueListing(asked, entriesJson, partsJson);

    /// <summary>The listing a finished listing built, or why reading the manifest failed (or didn't finish).</summary>
    public static Result<ItemListing, string> ToItemListing(Result<ListingStep, string> step) =>
        ManifestListing.ToItemListing(step);

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

    /// <summary>
    /// The catalog with a joined part of the full manifest added to its excerpt (what it had stays), and the enhanced
    /// traits' other plugs it names recognised (an account's enhanced Slice is Slice's enhanced trait).
    /// </summary>
    private static RuleCatalog AddManifest(RuleCatalog catalog, ManifestExcerpt joined) =>
        EnhancedPerkRecognition.RecognizeEnhancedPerks(catalog with { Manifest = catalog.Manifest.AddExcerpt(joined) });

    private static string DescribeManifestProblem(string problem) =>
        $"Couldn't read Bungie's manifest ({problem}). Items Loopsmith doesn't know show by hash.";

    /// <summary>
    /// The design with its build file parsed and validated again against a catalog (one with more of the manifest: what
    /// it says of the weapons the file gives by hash is read again), its steps replayed.
    /// </summary>
    private static Result<DesignSession, string> UseCatalog(DesignSession session, RuleCatalog catalog) =>
        BuildFileParsing.ParseBuildFile(session.Design.Build, catalog.Manifest)
            .Bind(build => BuildValidation.ValidateBuild(build, catalog))
            .Map(validated => CreateSession(validated, session.Design));

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

    /// <summary>
    /// The design with a weapon's mod picked, or its mod socket emptied (<paramref name="weapon"/> 0-based, in the build's
    /// order): only that weapon's entry of the embedded build file changes (<c>mod:</c>); the steps replay with it.
    /// </summary>
    public static Result<DesignSession, string> PickWeaponMod(DesignSession session, int weapon, Optional<ItemHash> mod) =>
        EditWeapon(session, weapon, loadout => WeaponRolling.PickMod(session.Build.Catalog, loadout, mod));

    /// <summary>
    /// The mods weapon number <paramref name="weapon"/> takes, as its mod picker lists them: an enhanced one only when
    /// the player's copy of it is Tier 4 or 5 (<paramref name="profile"/>: after a Bungie login; without one, or a
    /// weapon they don't own, its tier isn't known). None when the excerpt doesn't say.
    /// </summary>
    public static ImmutableArray<WeaponModOption> ListWeaponMods(ValidatedBuild build, int weapon, Optional<BungieProfile> profile) =>
        FindWeapon(build, weapon).Match(
            found => WeaponRolling.ListModOptions(
                build.Catalog,
                found.Value,
                profile.Bind(owned => FindWeaponMarks(owned, found.Value)).Bind(marks => marks.Tier)),
            _ => ImmutableArray<WeaponModOption>.Empty);

    /// <summary>
    /// The enemy counts a step's ability or weapon offers from <paramref name="state"/>, each with what it is for: up to the
    /// last count that still fills something (a buff to its cap, an ability's energy), the counts the equipped rules need
    /// from it in one action ("2 in 3 s · Arc Siphon", "12 · Synergy") and the action's own count.
    /// </summary>
    public static ImmutableArray<TargetCountChoice> ListTargetCountChoices(ValidatedBuild build, GameState state, PlayerAction action) =>
        TargetCountListing.ListTargetCountChoices(build, state, action);

    /// <summary>
    /// The enemy counts the build's equipped rules need in one action: a kill or hit of at least N (Synergy's 12 kills).
    /// </summary>
    public static ImmutableArray<TargetCount> ListNamedTargetCounts(ValidatedBuild build) =>
    [
        .. build.Equipped
            .SelectMany(equipped => equipped.Element.Rules)
            .SelectMany(rule => rule.On.Match(
                abilityCast: _ => ImmutableArray<TargetCount>.Empty,
                killAny: _ => [],
                killOfTier: _ => [],
                killDebuffed: _ => [],
                killMultiple: kill => [kill.AtLeast],
                damage: _ => [],
                damageDebuffed: _ => [],
                damageMultiple: damage => [damage.AtLeast],
                pickUp: _ => [],
                buffGained: _ => [],
                stacksMaxed: _ => [],
                finisher: _ => [],
                abilityCastAs: _ => [],
                killFewer: _ => [],   // fewer than N: 1 to 9 already reach it
                criticalHealth: _ => [],
                shieldBroken: _ => [],
                energyExpended: _ => [],
                damageFewer: _ => [],   // fewer than N: the counts below already reach it
                killBetween: kill => [kill.AtLeast],
                reload: _ => [],
                hitAtCriticalHealth: _ => [],
                pickUpAmmo: _ => [],
                readyWeapon: _ => []))
            .Distinct(),
    ];

    /// <summary>The build's armor sets: per set, the pieces worn (when the build says) and which of its perks are active.</summary>
    public static ImmutableArray<ArmorSetStatus> ListArmorSets(ValidatedBuild build) =>
        ArmorSetting.ListArmorSets(build.Catalog, build.Build);

    /// <summary>The build's weapons (by index) that hold this element: a perk they roll or have, their turned-on catalyst or their mod.</summary>
    public static ImmutableArray<int> ListWeaponsHolding(ValidatedBuild build, ElementId element) =>
        BuildValidation.ListWeaponsHolding(build.Catalog, build.Build, element);

    /// <summary>The build's artifact and its perks of it: the excerpt's artifact that unlocks the most of them; none on a tie.</summary>
    public static Optional<BuildArtifact> FindArtifact(ValidatedBuild build) =>
        ArtifactFinding.FindArtifact(build.Catalog, build.Build);

    // ── The artifact: which one and its perks (docs/loop-format.md, "Artifact") ───────────────────────────────

    /// <summary>Every artifact the build can equip (the excerpt's, with perk slots), by name.</summary>
    public static ImmutableArray<ManifestItem> ListArtifacts(ValidatedBuild build) =>
        ArtifactEditing.ListArtifacts(build.Catalog);

    /// <summary>An artifact's tiers and perk slots, when the excerpt has it.</summary>
    public static Optional<ManifestKind.Artifact> FindArtifactTiers(ValidatedBuild build, ItemHash artifact) =>
        ArtifactEditing.FindArtifact(build.Catalog, artifact);

    /// <summary>The build's artifact and the perk in each of its slots, by manifest hash (no artifact: no slots).</summary>
    public static ArtifactChoice ReadArtifactChoice(ValidatedBuild build) =>
        ArtifactEditing.ReadChoice(build.Catalog, build.Build);

    /// <summary>The slots to show for an artifact: the last picked for it here, else the build's own for its artifact, else empty ones.</summary>
    public static ImmutableArray<Optional<ItemHash>> RecallArtifactSlots(ValidatedBuild build, ImmutableDictionary<ItemHash, ImmutableArray<Optional<ItemHash>>> remembered, ItemHash artifact) =>
        ArtifactEditing.RecallSlots(build.Catalog, build.Build, remembered, artifact);

    /// <summary>The tier a perk is in (1-based); none for a perk the artifact doesn't have.</summary>
    public static Optional<int> FindArtifactTier(ManifestKind.Artifact artifact, ItemHash perk) =>
        ArtifactEditing.FindTier(artifact, perk);

    /// <summary>What an artifact's slot number <paramref name="slot"/> (0-based) can hold: the perks of the tiers it takes, Tier 1's first.</summary>
    public static ImmutableArray<ItemHash> ListArtifactSlotOptions(ManifestKind.Artifact artifact, int slot) =>
        ArtifactEditing.ListSlotOptions(artifact, slot);

    /// <summary>The rules' artifact perk with this hash; none when the rules don't know it (⊘).</summary>
    public static Optional<BuildElement> FindArtifactPerkElement(ValidatedBuild build, ItemHash perk) =>
        ArtifactEditing.FindPerkElement(build.Catalog, perk);

    /// <summary>
    /// The design with this artifact and these perks: only the embedded build file's artifact entries change
    /// (<c>artifact</c>, <c>artifactPerks</c> and the perks the rules don't know in <c>leftOut</c>); the build check runs
    /// again and the steps replay.
    /// </summary>
    public static Result<DesignSession, string> EditArtifact(DesignSession session, ArtifactChoice choice) =>
        RebuildWith(session, ArtifactEditing.ApplyChoice(session.Build.Catalog, session.Build.Build, choice)
            .Bind(build => BuildFileEditing.ReplaceArtifact(session.Design.Build, build)));

    /// <summary>The build's set bonuses in none of the manifest excerpt's sets (shown on their own).</summary>
    public static ImmutableArray<ElementId> ListLooseSetBonuses(ValidatedBuild build) =>
        ArmorSetting.ListLooseBonuses(build.Catalog, build.Build);

    /// <summary>
    /// What an element sets off in the build — each trigger, then its outcomes and reasons — for an item's details; one
    /// the build doesn't equip (a catalyst that is off, a set perk not active) as one copy would.
    /// </summary>
    public static ImmutableArray<StyledLine> ExplainElement(ValidatedBuild build, BuildElement element) =>
        BuildExplaining.RenderElementExplanation(build.Catalog.Glossary, BuildExplaining.ExplainElement(build, element));

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
        RebuildWith(session, FindWeapon(session.Build.Build, weapon)
            .Bind(change)
            .Bind(changed => BuildFileEditing.ReplaceWeapon(session.Design.Build, weapon, changed)));

    /// <summary>The design with its embedded build file's text changed: the build parsed and validated again, the steps replayed.</summary>
    private static Result<DesignSession, string> RebuildWith(DesignSession session, Result<string, string> text) =>
        text.Map(changed => session.Design.Build with { Text = changed })
            .Bind(file => BuildFileParsing.ParseBuildFile(file, session.Build.Catalog.Manifest)
                .Bind(build => BuildValidation.ValidateBuild(build, session.Build.Catalog))
                .Map(validated => CreateSession(validated, session.Design with { Build = file })));

    // ── The subclass: its abilities, aspects and fragments (docs/loop-format.md, "Subclass") ──────────────────

    /// <summary>True when the designer can edit the build's subclass: the manifest excerpt lists the plugs it can hold.</summary>
    public static bool CanEditSubclass(ValidatedBuild build) =>
        SubclassEditing.CanEdit(build.Catalog, build.Build);

    /// <summary>The plugs the build's subclass holds, by manifest hash (the rules' elements and the left-out plugs alike).</summary>
    public static SubclassPlugs ReadSubclassPlugs(ValidatedBuild build) =>
        SubclassEditing.ReadPlugs(build.Catalog, build.Build);

    /// <summary>The rules' element a subclass plug is on this build; none when the rules don't know it for its class (it is left out, ⊘).</summary>
    public static Optional<BuildElement> FindSubclassElement(ValidatedBuild build, ItemHash plug) =>
        SubclassEditing.FindElement(build.Catalog, build.Build, plug);

    /// <summary>Everything the build's subclass can hold in a slot (the manifest's), in its order.</summary>
    public static ImmutableArray<ItemHash> ListSubclassOptions(ValidatedBuild build, SubclassSlot slot) =>
        SubclassEditing.ListOptions(build.Catalog, build.Build, slot);

    /// <summary>How many fragment slots these aspects open; not known when an aspect's count isn't.</summary>
    public static FragmentSlotCount CountFragmentSlots(ValidatedBuild build, ImmutableArray<ItemHash> aspects) =>
        SubclassEditing.CountFragmentSlots(build.Catalog, aspects);

    /// <summary>What the subclass editor warns when the plugs hold more fragments than their aspects open; none when they fit.</summary>
    public static Optional<string> DescribeFragmentOverflow(ValidatedBuild build, SubclassPlugs plugs) =>
        SubclassEditing.DescribeFragmentOverflow(build.Catalog, plugs);

    /// <summary>The plugs with other aspects: the fragments the new slot count closes are taken out, the last ones first, and named.</summary>
    public static SubclassTrim SetSubclassAspects(ValidatedBuild build, SubclassPlugs plugs, ImmutableArray<ItemHash> aspects) =>
        SubclassEditing.SetAspects(build.Catalog, plugs, aspects);

    /// <summary>
    /// The design with its subclass holding these plugs: only the embedded build file's subclass entries change (its
    /// abilities, aspects, fragments and the plugs the rules don't know); the build check runs again and the steps
    /// replay. Its stats are added up again from the player's armor when they were (they equal what that armor gave for
    /// the old fragments); stats typed stay as typed.
    /// </summary>
    public static Result<DesignSession, string> EditSubclass(DesignSession session, Optional<BungieProfile> profile, SubclassPlugs plugs)
    {
        var addedUp = profile.Bind(owned => StatsAreAddedUp(session, owned) ? profile : Optional.None<BungieProfile>());
        return SubclassEditing.ApplyPlugs(session.Build.Catalog, session.Build.Build, plugs)
            .Bind(build => RebuildWith(session, BuildFileEditing.ReplaceSubclass(session.Design.Build, build)))
            .Map(edited => addedUp.Match(owned => SetStatsFromArmor(edited, owned.Value).Match(set => set.Value, _ => edited), _ => edited));
    }

    /// <summary>True when the build's stats are what its armor adds up to for the player (not typed): then a change to its fragments adds them up again.</summary>
    private static bool StatsAreAddedUp(DesignSession session, BungieProfile profile)
    {
        var choices = StatAdding.ListStatChoices(session.Build.Catalog, session.Build.Build, profile);
        return StatAdding.AddUpStats(session.Build.Catalog, session.Build.Build, choices)
            .Match(totals => totals.Value.Stats == session.Build.Build.Stats, _ => false);
    }

    // ── Stats: typed, or added up from the player's own armor (docs/loop-format.md, "Stats") ─────────────────

    /// <summary>
    /// The design with the build's stats set: only the embedded build file's <c>stats:</c> changes (an unknown stat is
    /// left out, "?"); the build check runs again (its stat checks with it) and the steps replay.
    /// </summary>
    public static Result<DesignSession, string> SetStats(DesignSession session, StatLine stats) =>
        RebuildWith(session, BuildFileEditing.ReplaceStats(session.Design.Build, stats));

    /// <summary>The six stats in the order the Stats section's inputs are in (the game's).</summary>
    public static ImmutableArray<StatKind> StatInputOrder => StatInputReading.InputOrder;

    /// <summary>A stat as typed: empty for unknown ("?"), else a whole number 0..200; or why it isn't one.</summary>
    public static Result<Optional<StatValue>, string> ReadStatInput(StatKind stat, string text) =>
        StatInputReading.ReadStatInput(stat, text);

    /// <summary>The Stats section's six inputs (in <see cref="StatInputOrder"/>) as a stat line, or what is wrong with each one that isn't a stat.</summary>
    public static Result<StatLine, ImmutableArray<string>> ReadStatInputs(ImmutableArray<string> inputs) =>
        StatInputReading.ReadStatInputs(inputs);

    /// <summary>Bungie's login page for this app (a public OAuth client): it comes back to the registered redirect URL with a code and the state.</summary>
    public static string ToBungieLoginUrl(string clientId, string state) =>
        BungieAuthorizing.ToAuthorizeUrl(clientId, state);

    /// <summary>Where the login's code becomes an access token.</summary>
    public const string BungieTokenUrl = BungieAuthorizing.TokenUrl;

    /// <summary>The token request's form (a public client: its id, no secret).</summary>
    public static ImmutableArray<KeyValuePair<string, string>> ToBungieTokenForm(string clientId, string code) =>
        BungieAuthorizing.ToTokenForm(clientId, code);

    /// <summary>The login page's answer, as the host passes it on: the code when the state is the one sent, else why there is none.</summary>
    public static Result<string, string> ReadBungieCallback(string answerJson, string sentState) =>
        BungieAuthorizing.ReadCallback(answerJson, sentState);

    /// <summary>The token endpoint's answer: the access token, or what Bungie said.</summary>
    public static Result<string, string> ReadBungieAccessToken(string json) =>
        BungieAuthorizing.ReadAccessToken(json);

    /// <summary>Bungie's answer to a request (its status and text): the text to read, or that Bungie is down.</summary>
    public static Result<string, string> ReadBungieAnswer(int status, string text) =>
        BungieProfileReading.ReadAnswer(status, text);

    /// <summary>What to say when the browser couldn't reach Bungie (<paramref name="why"/>: what it said).</summary>
    public static string DescribeBungieUnreachable(string why) =>
        BungieProfileReading.DescribeUnreachable(why);

    /// <summary>The logged-in player's account and memberships, on Bungie's API.</summary>
    public const string BungieMembershipsUrl = BungieProfileReading.MembershipsUrl;

    /// <summary>The Destiny membership to read (the cross-save primary one), or why there is none.</summary>
    public static Result<DestinyMembership, string> ReadDestinyMembership(string json) =>
        BungieProfileReading.ReadMembership(json);

    /// <summary>The membership's profile, with the parts Loopsmith reads (vault, characters, their items, instances, sockets).</summary>
    public static string ToBungieProfileUrl(DestinyMembership membership) =>
        BungieProfileReading.ToProfileUrl(membership);

    /// <summary>The profile's armor, or what Bungie said.</summary>
    public static Result<BungieProfile, string> ReadBungieProfile(string json) =>
        BungieProfileReading.ReadProfile(json);

    /// <summary>For each piece the build wears, the player's copies of it (the most points first).</summary>
    public static StatChoices ListStatChoices(ValidatedBuild build, BungieProfile profile) =>
        StatAdding.ListStatChoices(build.Catalog, build.Build, profile);

    /// <summary>The build's stats added up from the copies it wears (else the player's best), as DIM adds them up; or why they can't be.</summary>
    public static Result<StatTotals, string> AddUpStats(ValidatedBuild build, StatChoices choices) =>
        StatAdding.AddUpStats(build.Catalog, build.Build, choices);

    // ── The player's own armor in the build (docs/loop-format.md, "Stats") ──────────────────────────────────

    /// <summary>
    /// The design once the player has logged in, and what to tell them: each weapon whose copy is theirs (one picked
    /// from their account) holds that copy's perks and catalyst; when the build's armor copies are all theirs, its stats are set
    /// from them; else they're told to pick their own pieces and weapons.
    /// </summary>
    public static BungieLogin ApplyBungieLogin(DesignSession session, BungieProfile profile)
    {
        var copies = CopyFinding.ListOwnedCopies(session.Build.Build, profile);
        var loaded = WeaponSwapping.LoadCopies(session.Build.Catalog, session.Build.Build, copies);
        var armed = LoadWeaponCopies(session, loaded.Build, loaded.Changed);
        var ownership = StatAdding.ReadOwnership(session.Build.Catalog, session.Build.Build, profile);
        var weapons = armed.Match(_ => CopyFinding.DescribeLoadedWeapons(session.Build.Build, loaded.Loaded), refused => refused.Failure);
        var message = string.Join(" ", new[] { StatAdding.DescribeOwnership(ownership), weapons }.Where(part => part.Length > 0));
        var current = armed.Match(ok => ok.Value, _ => session);
        return ownership is LoadoutOwnership.Own
            ? SetStatsFromArmor(current, profile).Match(
                set => new BungieLogin(set.Value, message),
                refused => new BungieLogin(current, refused.Failure))
            : new BungieLogin(current, message);
    }

    /// <summary>The design with its weapons <paramref name="changed"/> (by weapon number) as <paramref name="build"/> has them: only those entries of the embedded build change.</summary>
    private static Result<DesignSession, string> LoadWeaponCopies(DesignSession session, Build build, ImmutableArray<int> changed)
    {
        var file = session.Design.Build;
        return changed.IsEmpty
            ? new Result<DesignSession, string>.Ok(session)
            : RebuildWith(session, changed.Aggregate(
                new Result<string, string>.Ok(file.Text) as Result<string, string>,
                (text, index) => text.Bind(current => BuildFileEditing.ReplaceWeapon(file with { Text = current }, index, build.Weapons[index]))));
    }

    /// <summary>
    /// The design wearing the player's copy <paramref name="instance"/> in <paramref name="slot"/>: the embedded build file's
    /// exotic, left-out pieces, set bonuses and copies change (ArmorSwapping), the build check runs again and the steps
    /// replay; then its stats are set from its armor when they add up. Or why the piece can't be worn.
    /// </summary>
    public static Result<DesignSession, string> PickArmorPiece(DesignSession session, BungieProfile profile, ArmorSlot slot, ItemInstanceId instance) =>
        StatAdding.FindArmor(profile, instance)
            .Bind(armor => ArmorSwapping.SwapPiece(session.Build.Catalog, session.Build.Build, slot, armor.Hash, instance))
            .Bind(build => RebuildWith(session, BuildFileEditing.ReplaceArmor(session.Design.Build, build)))
            .Map(worn => SetStatsFromArmor(worn, profile).Match(set => set.Value, _ => worn));

    /// <summary>
    /// What the armor picker lists for a slot of the build: the player's pieces for it that the build can wear (its class,
    /// one exotic: ArmorSwapping), and notes on what it leaves out.
    /// </summary>
    public static ArmorOptions ListArmorOptions(ValidatedBuild build, BungieProfile profile, ArmorSlot slot)
    {
        var listed = StatAdding.ListArmorOptions(build.Catalog, build.Build, profile, slot);
        var exotic = ArmorSwapping.DescribeExoticRule(build.Catalog, build.Build, slot);
        return listed with
        {
            Options = [.. listed.Options.Where(option => ArmorSwapping.CheckPiece(build.Catalog, build.Build, slot, option.Armor.Hash).IsOk())],
            Notes = [.. exotic.Match(note => new[] { note.Value }, _ => []), .. listed.Notes],
        };
    }

    /// <summary>The player's armor and weapons the catalog's excerpt lacks (what the full manifest is read for, so the pickers can name and place them).</summary>
    public static ImmutableArray<ItemHash> ListUnknownProfileHashes(DesignSession session, BungieProfile profile) =>
        ManifestJoin.ListUnknown(
            session.Build.Catalog.Manifest,
            profile.Armor.Select(armor => armor.Hash).Concat(profile.Weapons.Select(weapon => weapon.Hash)).Concat(profile.Finishers.Select(finisher => finisher.Hash)).Distinct());

    // ── The player's own weapons in the build (docs/loop-format.md, "Weapons from your account") ─────────────

    /// <summary>
    /// What the weapon picker lists for where <paramref name="target"/> says (a weapon of the build, or an empty slot): the
    /// player's weapons for that slot that the build can wear there (one exotic: WeaponSwapping), the copy the build names
    /// first; with notes on the exotic rule and on the weapons Loopsmith can't place yet.
    /// </summary>
    public static WeaponOptions ListWeaponOptions(ValidatedBuild build, BungieProfile profile, WeaponTarget target)
    {
        var listed = CopyFinding.ListWeaponOptions(build.Catalog, build.Build, profile, target);
        var exotic = WeaponSwapping.DescribeExoticRule(build.Catalog, build.Build, target);
        return listed with
        {
            Options = [.. listed.Options.Where(option => WeaponSwapping.CheckWeapon(build.Catalog, build.Build, target, option.Weapon.Hash).IsOk())],
            Notes = [.. exotic.Match(note => new[] { note.Value }, _ => []), .. listed.Notes],
        };
    }

    /// <summary>
    /// The design wearing the player's weapon <paramref name="instance"/> where <paramref name="target"/> says: in place of
    /// one of its weapons (that entry of the embedded build file is written anew with the copy's perks, catalyst and
    /// instance), or added to an empty slot (WeaponSwapping); the build check runs again and the steps replay. Or why the
    /// weapon can't be worn.
    /// </summary>
    public static Result<DesignSession, string> PickWeapon(DesignSession session, BungieProfile profile, WeaponTarget target, ItemInstanceId instance) =>
        CopyFinding.FindWeapon(profile, instance)
            .Bind(copy => WeaponSwapping.SwapWeapon(session.Build.Catalog, session.Build.Build, target, copy))
            .Bind(build => RebuildWith(session, target.Match(
                inPlace => BuildFileEditing.ReplaceWeapon(session.Design.Build, inPlace.Weapon, build.Weapons[inPlace.Weapon]),
                _ => BuildFileEditing.AddWeapon(session.Design.Build, build.Weapons[^1]))));

    // ── Every weapon and armor piece in the game, and emptying a slot (docs/loop-format.md, "Every item") ──────

    /// <summary>
    /// What the weapon picker's "Every item" lists for where <paramref name="target"/> says: every weapon of the game for
    /// that slot the build can wear there (one exotic), the one it wears there first, then by name.
    /// </summary>
    public static WeaponItemOptions ListWeaponItems(ValidatedBuild build, ItemListing listing, WeaponTarget target) =>
        WeaponSwapping.ListItemOptions(build.Catalog, build.Build, listing, target);

    /// <summary>
    /// What the armor picker's "Every item" lists for <paramref name="slot"/>: every piece of the game for that slot and
    /// the build's class (one exotic), the one it wears there first, then by name, each with its set.
    /// </summary>
    public static ArmorItemOptions ListArmorItems(ValidatedBuild build, ItemListing listing, ArmorSlot slot) =>
        ArmorSwapping.ListItemOptions(build.Catalog, build.Build, listing, slot);

    /// <summary>The item picked from "Every item", when the catalog's excerpt lacks it (what the full manifest is read for before the pick: its perks, catalyst or set).</summary>
    public static ImmutableArray<ItemHash> ListUnknownItemHashes(DesignSession session, ItemHash item) =>
        ManifestJoin.ListUnknown(session.Build.Catalog.Manifest, [item]);

    /// <summary>
    /// The design with what Bungie's full manifest said of an item picked from "Every item" (its perks, catalyst or set:
    /// <see cref="UseJoinedManifest"/>), or, when it couldn't be read, why — the pick waits for it, so it doesn't happen.
    /// </summary>
    public static Result<DesignSession, string> UsePickedItemManifest(DesignSession session, Result<ManifestExcerpt, string> joined) =>
        joined.Match(
            _ => UseJoinedManifest(session, joined),
            failed => new Result<DesignSession, string>.Error($"Loopsmith couldn't read Bungie's manifest for it ({failed.Failure}). Try again."));

    /// <summary>
    /// The design wearing the weapon <paramref name="item"/> of the game where <paramref name="target"/> says: in place of
    /// one of its weapons (that entry of the embedded build file is written anew: no roll, its catalyst off), or added to
    /// an empty slot; the build check runs again and the steps replay. Or why the weapon can't be worn.
    /// </summary>
    public static Result<DesignSession, string> PickWeaponItem(DesignSession session, WeaponTarget target, ItemHash item) =>
        WeaponSwapping.PutWeapon(session.Build.Catalog, session.Build.Build, target, item)
            .Bind(build => RebuildWith(session, target.Match(
                inPlace => BuildFileEditing.ReplaceWeapon(session.Design.Build, inPlace.Weapon, build.Weapons[inPlace.Weapon]),
                _ => BuildFileEditing.AddWeapon(session.Design.Build, build.Weapons[^1]))));

    /// <summary>
    /// The design wearing the armor piece <paramref name="item"/> of the game in <paramref name="slot"/> (no copy: its
    /// stats stay as they were); the build check runs again and the steps replay. Or why the piece can't be worn.
    /// </summary>
    public static Result<DesignSession, string> PickArmorItem(DesignSession session, ArmorSlot slot, ItemHash item) =>
        ArmorSwapping.PutPiece(session.Build.Catalog, session.Build.Build, slot, item)
            .Bind(build => RebuildWith(session, BuildFileEditing.ReplaceArmor(session.Design.Build, build)));

    /// <summary>
    /// The design without its weapon number <paramref name="weapon"/>: that slot empty (a step that uses it is blocked);
    /// the build check runs again and the steps replay. Or why not.
    /// </summary>
    public static Result<DesignSession, string> EmptyWeaponSlot(DesignSession session, int weapon) =>
        WeaponSwapping.RemoveWeapon(session.Build.Build, weapon)
            .Bind(_ => RebuildWith(session, BuildFileEditing.RemoveWeapon(session.Design.Build, weapon)));

    /// <summary>
    /// The design with nothing in <paramref name="slot"/> (its exotic gone when it was there, its set bonuses following
    /// the pieces left); the build check runs again and the steps replay. Or why not.
    /// </summary>
    public static Result<DesignSession, string> EmptyArmorSlot(DesignSession session, ArmorSlot slot) =>
        ArmorSwapping.RemovePiece(session.Build.Catalog, session.Build.Build, slot)
            .Bind(build => RebuildWith(session, BuildFileEditing.ReplaceArmor(session.Design.Build, build)));

    // ── Armor mods: the pieces' mod sockets, the mod picker (docs/loop-format.md, "Armor mods") ──────────────

    /// <summary>
    /// The energy each armor slot's copy holds, after the player's Bungie login: the copy the build wears there (the
    /// one it names when theirs, else their own); a slot without one isn't in it ("?").
    /// </summary>
    public static ImmutableDictionary<ArmorSlot, ArmorEnergy> ListArmorCapacities(ValidatedBuild build, Optional<BungieProfile> profile) =>
        profile.Match(
            owned => Enum.GetValues<ArmorSlot>()
                .SelectMany(slot => CopyFinding.FindWornArmor(build.Catalog, build.Build, owned.Value, slot)
                    .Bind(armor => armor.EnergyCapacity)
                    .Bind(capacity => ArmorEnergy.TryFrom(capacity).IsSuccess ? Optional.Some(ArmorEnergy.From(capacity)) : Optional.None<ArmorEnergy>())
                    .Match(capacity => new[] { KeyValuePair.Create(slot, capacity.Value) }, _ => []))
                .ToImmutableDictionary(),
            _ => ImmutableDictionary<ArmorSlot, ArmorEnergy>.Empty);

    /// <summary>
    /// The build's mods in its pieces' sockets (from the listing of every piece): each slot's row (its slot mods and own
    /// sockets) with its energy, the General row, and the mods no socket takes.
    /// </summary>
    public static ModLayout LayOutMods(ValidatedBuild build, ItemListing listing, Optional<BungieProfile> profile) =>
        ArmorModding.LayOutMods(build.Catalog, build.Build, listing, ListArmorCapacities(build, profile));

    /// <summary>What the mod picker lists for a socket: every mod of its category, the subclass's element first, the ones that don't fit refused and why; or why there's no such socket.</summary>
    public static Result<ModOptions, string> ListModOptions(ValidatedBuild build, ItemListing listing, Optional<BungieProfile> profile, ModSocketPlace place) =>
        ArmorModding.ListModOptions(build.Catalog, build.Build, listing, ListArmorCapacities(build, profile), place);

    /// <summary>
    /// The design with <paramref name="mod"/> in the socket at <paramref name="place"/>: only the embedded build file's
    /// mod entries change (<c>armorMods</c>, <c>armorModPlugs</c>, the mods in <c>leftOut</c>); the build check runs again,
    /// the steps replay, and its stats are added up again from the player's armor when they were (a stat mod changes
    /// them). Or why the mod can't go in (its energy, its socket). Read the full manifest for a mod the excerpt lacks
    /// first (<see cref="ListUnknownItemHashes"/>), as for an item.
    /// </summary>
    public static Result<DesignSession, string> PickMod(DesignSession session, ItemListing listing, Optional<BungieProfile> profile, ModSocketPlace place, ItemHash mod) =>
        EditMods(session, profile, build => ArmorModding.PutMod(build.Catalog, build.Build, listing, ListArmorCapacities(build, profile), place, mod));

    /// <summary>The design without the mod in the socket at <paramref name="place"/> (as <see cref="PickMod"/> changes it); or why not.</summary>
    public static Result<DesignSession, string> ClearMod(DesignSession session, ItemListing listing, Optional<BungieProfile> profile, ModSocketPlace place) =>
        EditMods(session, profile, build => ArmorModding.ClearMod(build.Catalog, build.Build, listing, ListArmorCapacities(build, profile), place));

    private static Result<DesignSession, string> EditMods(DesignSession session, Optional<BungieProfile> profile, Func<ValidatedBuild, Result<Build, string>> change)
    {
        var addedUp = profile.Bind(owned => StatsAreAddedUp(session, owned) ? profile : Optional.None<BungieProfile>());
        return change(session.Build)
            .Bind(build => RebuildWith(session, BuildFileEditing.ReplaceMods(session.Design.Build, build)))
            .Map(edited => addedUp.Match(owned => SetStatsFromArmor(edited, owned.Value).Match(set => set.Value, _ => edited), _ => edited));
    }

    /// <summary>
    /// What the game draws on the player's copy the build wears in a slot (its tier, masterwork): the copy it names when it
    /// is theirs, else their own (the one the picker ticks); none when they own no copy of the piece.
    /// </summary>
    public static Optional<GearMarks> FindArmorMarks(ValidatedBuild build, BungieProfile profile, ArmorSlot slot) =>
        CopyFinding.FindWornArmor(build.Catalog, build.Build, profile, slot).Map(armor => armor.Marks);

    /// <summary>
    /// The stat a copy of the player's focuses on, with its name and icon: its highest rolled stat, as DIM shows it in an
    /// Armor 3.0 piece's top-right corner; none when its roll isn't read.
    /// </summary>
    public static Optional<ArmorStatIcon> FindStatFocus(ValidatedBuild build, OwnedArmor armor) =>
        StatAdding.FindStatFocus(build.Catalog, armor).Bind(build.Catalog.Manifest.FindArmorStat);

    /// <summary>The stat focus (<see cref="FindStatFocus(ValidatedBuild, OwnedArmor)"/>) of the player's copy the build wears in a slot.</summary>
    public static Optional<ArmorStatIcon> FindStatFocus(ValidatedBuild build, BungieProfile profile, ArmorSlot slot) =>
        CopyFinding.FindWornArmor(build.Catalog, build.Build, profile, slot).Bind(armor => FindStatFocus(build, armor));

    /// <summary>What the game draws on the player's copy of a build's weapon (its tier, masterwork, breaker): the copy it names when it is theirs, else their own.</summary>
    public static Optional<GearMarks> FindWeaponMarks(BungieProfile profile, WeaponLoadout weapon) =>
        CopyFinding.FindWornWeapon(profile, weapon).Map(owned => owned.Marks);

    /// <summary>
    /// The champion a build's weapon breaks, as DIM shows it: its copy's breaker (<paramref name="marks"/>, after the
    /// player's login), else the weapon's own (an exotic's: Thunderlord's Disruption; a legendary's frame's: Adaptive
    /// Frame pierces barriers); none when it has none.
    /// </summary>
    public static Optional<BreakerType> FindWeaponBreaker(ValidatedBuild build, WeaponLoadout weapon, Optional<GearMarks> marks) =>
        CopyFinding.FindWeaponBreaker(build.Catalog.Manifest, weapon, marks);

    /// <summary>
    /// A legendary weapon's intrinsic frame (Adaptive Frame), as the excerpt has it: DIM shows its icon in the weapon's
    /// top-right corner. None for an exotic (its intrinsic is its own perk, which DIM doesn't show there) or a weapon whose
    /// frame the excerpt lacks.
    /// </summary>
    public static Optional<ManifestItem> FindWeaponFrame(ValidatedBuild build, ManifestItem weapon) =>
        weapon.Kind is ManifestKind.Weapon kind && weapon.Tier != Optional.Some(ItemTier.Exotic)
            ? kind.Intrinsic.Bind(build.Catalog.Manifest.FindItem)
            : Optional.None<ManifestItem>();

    /// <summary>The champion one of the player's weapons breaks (its own breaker, else its weapon's): what the weapon picker shows.</summary>
    public static Optional<BreakerType> FindCopyBreaker(ValidatedBuild build, OwnedWeapon copy) =>
        CopyFinding.FindCopyBreaker(build.Catalog.Manifest, copy);

    /// <summary>The design with its stats set from the copies its build wears, or why they don't add up.</summary>
    private static Result<DesignSession, string> SetStatsFromArmor(DesignSession session, BungieProfile profile)
    {
        var choices = StatAdding.ListStatChoices(session.Build.Catalog, session.Build.Build, profile);
        return StatAdding.AddUpStats(session.Build.Catalog, session.Build.Build, choices)
            .Bind(totals => SetStats(session, totals.Stats));
    }

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
    /// Every step the build offers right now, grouped: every ability (always available — ADRs D5), each secondary use
    /// of one an equipped rule names (<see cref="ListSecondaryUses"/>) and, with the abilities, a finisher (always
    /// possible too), every weapon, the pickups on the ground and the
    /// states you can declare (ADRs D3). <see cref="TriggerOption.IsNew"/> marks what the last step made available.
    /// </summary>
    public static ImmutableArray<TriggerOption> ListTriggerOptions(DesignSession session)
    {
        var build = session.Build;
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
                build.DescribeAction(candidate.Action),
                candidate.Group,
                !previous.Contains(candidate.Action) && !session.Resolutions.IsEmpty,
                candidate.IsEnabled))
            .ToImmutableArray();
    }

    private static ImmutableArray<(PlayerAction Action, TriggerGroup Group, bool IsEnabled)> ListCandidateActions(ValidatedBuild build, GameState state)
    {
        var hits = new[] { HitOutcome.Kill, HitOutcome.Damage };
        var abilities = new[] { OffensiveAbility.Grenade, OffensiveAbility.Melee }
            .SelectMany(kind => ActionResolution.ListCastActions(build, kind))
            .Concat(ActionResolution.ListClassAbilityActions(build))
            .Concat(ActionResolution.ListCastActions(build, OffensiveAbility.Super))
            .Concat(ActionResolution.ListFinisherActions())
            .Select(action => (action, TriggerGroup.Ability, true));
        var weapons = build.Build.Weapons
            .SelectMany(weapon => hits
                .Select(hit => (PlayerAction)new PlayerAction.FireWeapon(weapon.Slot, hit, TargetCount.One))
                .Append(new PlayerAction.ReloadWeapon(weapon.Slot))   // a reload, the player's to declare (ADRs D37)
                .Select(action => (action, TriggerGroup.Weapon, true))
                .Append((new PlayerAction.ReadyWeapon(weapon.Slot), TriggerGroup.Weapon, ActionResolution.IsReadyReactedTo(build, weapon))));   // always shown, enabled when a rule reacts
        var bricks = ActionResolution.ListAmmoBrickActions()
            .Select(action => (action, TriggerGroup.AmmoBrick, true));   // always: bricks drop from enemies (ADRs D43)
        var pickups = state.ListPickups()
            .Select(pickup => ((PlayerAction)new PlayerAction.CollectPickups(pickup), TriggerGroup.Pickup, true));
        var declarations = ActionResolution.ListDeclarations(build, state)
            .Select(declaration => ((PlayerAction)new PlayerAction.Declare(declaration), TriggerGroup.Declare, true));
        return [.. abilities, .. weapons, .. bricks, .. pickups, .. declarations];
    }

    /// <summary>
    /// The secondary uses of an ability the build offers (Tempest Strike's sliding melee, Ascension's air move), each with
    /// the equipped elements whose rules name it; none when no rule tells a secondary apart from the ability's press.
    /// </summary>
    public static ImmutableArray<SecondaryUse> ListSecondaryUses(ValidatedBuild build, AbilityKind kind) =>
        ActionResolution.ListSecondaryUses(build, kind);

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

    /// <summary>
    /// The options a step can carry in this build (ADRs D36), each when it sets something off: "breaks a shield" on a weapon
    /// step when an equipped rule reacts to a broken shield, and "precision" when one reacts to a precision or a body kill (Spark
    /// of Brilliance, Hatchling); "spend fully" (its last charge) on a grenade, melee or class ability step when the build gives that
    /// ability more than one charge (Shinobu's Vow) and an equipped rule reacts to its energy fully expended — with one
    /// charge, every use is the last. None for any other step.
    /// </summary>
    public static ImmutableArray<StepOption> ListStepOptions(ValidatedBuild build, PlayerAction action) =>
        action switch
        {
            PlayerAction.FireWeapon =>
            [
                .. HasRule(build, rule => rule.On is Trigger.ShieldBroken) ? [StepOption.BreaksShield] : Array.Empty<StepOption>(),
                .. HasRule(build, rule => rule.On.NeedsKillZone()) ? [StepOption.Precision] : Array.Empty<StepOption>(),
            ],
            PlayerAction.CastAbility cast when CanSpendLastCharge(build, cast.Kind.ToAbilityKind()) => [StepOption.LastCharge],
            PlayerAction.UseClassAbility when CanSpendLastCharge(build, AbilityKind.ClassAbility) => [StepOption.LastCharge],
            PlayerAction.PickUpAmmo when HasRule(build, rule => rule.On is Trigger.PickUpAmmo { Needs: BrickNeed.Slide }) => [StepOption.Slide],
            _ => [],
        };

    /// <summary>True when the build gives the ability more than one charge and an equipped rule reacts to its last one.</summary>
    private static bool CanSpendLastCharge(ValidatedBuild build, AbilityKind kind) =>
        build.CountCharges(kind) != ChargeCount.One && HasRule(build, rule => rule.On is Trigger.EnergyExpended expended && expended.Kind == kind);

    private static bool HasRule(ValidatedBuild build, Func<Rule, bool> matches) =>
        build.Equipped.Any(equipped => equipped.Element.Rules.Any(matches));

    /// <summary>
    /// The same step with the option on or off (ADRs D36): a weapon step that breaks a shield or not, whose kills are precision
    /// ones or not, an ability step that
    /// spends the last charge or a plain one. A step the option isn't for comes back unchanged.
    /// </summary>
    public static PlayerAction SetStepOption(PlayerAction action, StepOption option, bool isOn) =>
        (action, option) switch
        {
            (PlayerAction.FireWeapon fire, StepOption.BreaksShield) => fire with { Shield = isOn ? ShieldBreak.Breaks : ShieldBreak.None },
            (PlayerAction.FireWeapon fire, StepOption.Precision) => fire with { Zone = isOn ? HitZone.Precision : HitZone.Body },
            (PlayerAction.CastAbility cast, StepOption.LastCharge) => cast with { Charge = isOn ? ChargeUse.Last : ChargeUse.Plain },
            (PlayerAction.UseClassAbility use, StepOption.LastCharge) => use with { Charge = isOn ? ChargeUse.Last : ChargeUse.Plain },
            (PlayerAction.PickUpAmmo ammo, StepOption.Slide) => ammo with { Pickup = isOn ? BrickPickup.Slide : BrickPickup.Plain },
            _ => action,
        };

    /// <summary>
    /// True when an equipped rule reacts to the rank of the enemy a kill kills (<c>kill: { tier: elite+ }</c>: Ascendant Escape):
    /// the designer's rank picker on a kill row then sets something off, and the finisher takes a rank too (ADRs D45).
    /// </summary>
    public static bool ReactsToRank(ValidatedBuild build) =>
        HasRule(build, rule => rule.On is Trigger.KillOfTier);

    /// <summary>The ranks a kill step can say it kills, weakest first: the pack's minors, then elite, miniboss, champion, boss.</summary>
    public static ImmutableArray<EnemyTier> ListRanks() =>
        [.. Enum.GetValues<EnemyTier>()];

    /// <summary>
    /// The same step killing an enemy of that rank (ADRs D45): an ability's or weapon's kill, or a finisher. A hit, or any
    /// other step, comes back unchanged: only a kill has a rank.
    /// </summary>
    public static PlayerAction SetKillRank(PlayerAction action, EnemyTier rank) =>
        action switch
        {
            PlayerAction.CastAbility { Hit: HitOutcome.Kill } cast => cast with { Rank = rank },
            PlayerAction.FireWeapon { Hit: HitOutcome.Kill } fire => fire with { Rank = rank },
            PlayerAction.PerformFinisher finisher => finisher with { Rank = rank },
            _ => action,
        };

    // ── export, import, analyse (LoopFiles, StepPlaying.LoopPlaying, LoopAnalyzing) ──

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
            .Bind(design => BuildFileParsing.ParseBuildFile(design.Build, catalog.Manifest)
                .Bind(build => BuildValidation.ValidateBuild(build, catalog))
                .Map(validated => CreateSession(validated, design)));

    /// <summary>
    /// The design analysed by its order of triggers: played from a fresh spawn, then again from where it ended until
    /// it repeats — what each step needs and from which step, what it sets off, what is wasted (ADRs D2).
    /// </summary>
    public static LoopReport AnalyzeDesign(DesignSession session)
    {
        var played = LoopPlaying.PlayLoop(session.Build, session.Design.Steps);
        return LoopAnalysis.AnalyzeLoop(session.Build, session.Design, played);
    }

    /// <summary>
    /// The build's own validation issues (warnings, info) plus, as Info, a loop designed against a catalog other
    /// than the one it is replayed with.
    /// </summary>
    public static ImmutableArray<BuildIssue> ListDesignIssues(DesignSession session) =>
        [.. session.Build.Issues, .. FindCatalogMismatch(session).Match(issue => [issue.Value], _ => ImmutableArray<BuildIssue>.Empty)];

    /// <summary>
    /// The design's issues (<see cref="ListDesignIssues(DesignSession)"/>) and, after the player's Bungie login, what a
    /// weapon whose copy is theirs holds that the copy doesn't have: one warning line per weapon — the line on its roll's
    /// perks the rules don't know, when there is one, says it too (CopyPerkChecking).
    /// </summary>
    public static ImmutableArray<BuildIssue> ListDesignIssues(DesignSession session, Optional<BungieProfile> profile) =>
        profile.Match(
            owned => CopyPerkChecking.AddCopyWarnings(session.Build.Catalog, ListDesignIssues(session), ListCopyColumns(session.Build, owned.Value)),
            _ => ListDesignIssues(session));

    /// <summary>
    /// The perks a build's weapon's columns that roll offer that the player's copy doesn't have there (the perk picker
    /// marks them), when the weapon's copy (its instance: picked from theirs) is in their
    /// account; none for any other weapon. A copy has each perk of a column that rolled two or more, and the base perk of
    /// an enhanced one (CopyPerkChecking).
    /// </summary>
    public static ImmutableArray<ColumnPerk> ListPerksNotOnCopy(ValidatedBuild build, BungieProfile profile, WeaponLoadout weapon) =>
        CopyFinding.FindOwnCopy(profile, weapon).Match(
            copy => CopyPerkChecking.ListMissingPerks(build.Catalog, weapon, CopyFinding.ReadCopyColumns(build.Catalog.Manifest, weapon, copy.Value)),
            _ => ImmutableArray<ColumnPerk>.Empty);

    /// <summary>The build's weapons whose copies are the player's, each with the perks its copy has by column.</summary>
    private static ImmutableArray<(WeaponLoadout Weapon, ImmutableArray<ImmutableArray<ItemHash>> Columns)> ListCopyColumns(ValidatedBuild build, BungieProfile profile) =>
    [
        .. CopyFinding.ListOwnedCopies(build.Build, profile).Select(owned =>
            (build.Build.Weapons[owned.Weapon], CopyFinding.ReadCopyColumns(build.Catalog.Manifest, build.Build.Weapons[owned.Weapon], owned.Copy))),
    ];

    /// <summary>Info when the loop was designed against another catalog version than the one replaying it.</summary>
    public static Optional<BuildIssue> FindCatalogMismatch(DesignSession session)
    {
        var current = session.Build.Catalog.Version;
        return session.Design.Catalog.Bind(designed => designed == current
            ? Optional.None<BuildIssue>()
            : Optional.Some(new BuildIssue(Severity.Info, $"Loop designed against catalog {designed}; replaying with {current}.")));
    }

    /// <summary>
    /// What one step fired as a tree: each event, the elements it set off, each of their outcomes on a line of its own
    /// (the trace's wording), and the keywords they name.
    /// </summary>
    public static StepOutcomes ShapeStepOutcomes(DesignSession session, Resolution resolution, TraceOptions options) =>
        StepOutcomeShaping.ShapeStepOutcomes(session.Build, resolution, options);

    /// <summary>
    /// What is present at the end of a step, laid out like the game's HUD: buffs on you, buffs on your weapons, debuffs
    /// on the pack, pickups on the ground, the passives that hold (the designer draws it under the step).
    /// </summary>
    public static StepState ShapeStepState(DesignSession session, Resolution resolution) =>
        StepStateShaping.ShapeStepState(session.Build, resolution.State, resolution.ActivePassives);

    /// <summary>
    /// A step's energy (ADRs D48): the grenade's, melee's and class ability's before and after it, and whether the ability
    /// it uses was ready (the designer flags one that wasn't).
    /// </summary>
    public static StepEnergy ShapeStepEnergy(DesignSession session, Resolution resolution) =>
        StepEnergyShaping.ShapeStepEnergy(session.Build, resolution);

    /// <summary>
    /// What in this build interacts with a keyword, for its details, role by role: Armor Charge — Elemental Charge gives
    /// it, Grenade Kickstart uses it up; Amplified — Flow State gives it, and while Amplified Spark of Frequency's extra
    /// Bolt Charge (and Ionic Overclock, on which weapons).
    /// </summary>
    public static ImmutableArray<InteractionGroup> ExplainInteractions(ValidatedBuild build, KeywordRef keyword) =>
        KeywordInteracting.ExplainInteractions(build, keyword);

    /// <summary>
    /// What an element is, as a label: a keyword by what it is in the game ("Arc buff", "Arc elemental pickup", "Pickup"),
    /// anything else by its kind ("Fragment", "Armor mod").
    /// </summary>
    public static string DescribeElementType(ValidatedBuild build, ElementId element) =>
        KeywordInteracting.DescribeElementType(build.Catalog, element);

    /// <summary>What a left-out item is: a gap (no rule knows it yet) or something with no part in a loop, and why.</summary>
    public static LeftOutReason ClassifyLeftOut(ValidatedBuild build, LeftOutItem item) =>
        LeftOutClassifying.ClassifyLeftOut(build.Catalog, item);

    /// <summary>The left-out items no rule knows yet: what "Not in Loopsmith yet" lists.</summary>
    public static ImmutableArray<LeftOutItem> ListMissing(ValidatedBuild build) =>
        LeftOutClassifying.ListMissing(build.Catalog, build.Build);

    /// <summary>The icon of what a step does: its weapon's, its ability's, or its pickup's or status's keyword icon.</summary>
    public static Optional<IconRef> FindActionIcon(ValidatedBuild build, PlayerAction action) =>
        IconFinding.FindActionIcon(build.Catalog, build.Build, action);

    /// <summary>
    /// As <see cref="FindActionIcon(ValidatedBuild, PlayerAction)"/>, and a finisher's is the player's equipped finisher's
    /// after a Bungie login (<see cref="FindFinisher"/>).
    /// </summary>
    public static Optional<IconRef> FindActionIcon(ValidatedBuild build, Optional<BungieProfile> profile, PlayerAction action) =>
        action is PlayerAction.PerformFinisher
            ? FinisherFinding.FindFinisher(build.Catalog, build.Build, profile).Bind(item => item.Icon).Map(IconFinding.ToManifestIcon)
            : IconFinding.FindActionIcon(build.Catalog, build.Build, action);

    /// <summary>
    /// The build's finisher (its name and icon): the one the player's character of the build's class has equipped, after
    /// a Bungie login; else the class's own (Blade Carnival for a Hunter); none when the excerpt has neither.
    /// </summary>
    public static Optional<ManifestItem> FindFinisher(ValidatedBuild build, Optional<BungieProfile> profile) =>
        FinisherFinding.FindFinisher(build.Catalog, build.Build, profile);

    /// <summary>A keyword's icon: the game's own (Jolted, Woven Mail), else its element's, else the shape the game draws for it (Unraveling Rounds); none without any.</summary>
    public static Optional<IconRef> FindKeywordIcon(ValidatedBuild build, KeywordRef keyword) =>
        build.Catalog.FindKeywordIcon(keyword);

    /// <summary>
    /// Whether a weapon perk is an enhanced trait, which the game and DIM draw over the base perk's icon (Bungie gives
    /// both the same image): a plug Bungie types "Enhanced Trait" or "Enhanced Origin Trait", or one of an enhanced
    /// element of the rules (Slice (enhanced)).
    /// </summary>
    public static bool IsEnhancedPerk(RuleCatalog catalog, ItemHash perk) =>
        ManifestReading.IsEnhancedPerk(catalog, perk);

    /// <summary>Whether an element is a weapon perk's enhanced trait (Slice (enhanced)): see <see cref="IsEnhancedPerk(RuleCatalog, ItemHash)"/>.</summary>
    public static bool IsEnhancedPerk(RuleCatalog catalog, ElementId element) =>
        catalog.EnhancedPerks.ContainsValue(element);

    /// <summary>An element's icon: its manifest item's, else (a keyword's element: Jolt, Bolt Charge) the keyword's own; none when there is neither.</summary>
    public static Optional<IconRef> FindElementIcon(RuleCatalog catalog, ElementId element) =>
        IconFinding.FindElementIcon(catalog, element);

    /// <summary>The keyword an element is (Bolt Charge, Ionic Trace), when it is one.</summary>
    public static Optional<KeywordRef> FindKeyword(ValidatedBuild build, ElementId element) =>
        build.Catalog.FindElementKeyword(element);

    /// <summary>The build's weapons (by index) whose own perks give this buff: Slice is Festival Flight's.</summary>
    public static ImmutableArray<int> ListWeaponsGiving(ValidatedBuild build, StatusId buff) =>
        BuildValidation.ListWeaponsGiving(build.Catalog, build.Build, buff);

    /// <summary>
    /// The weapons a buff shows with: those holding the element it shows (the enhanced Stats for All: the weapon that shot,
    /// not one rolling the base perk); when none holds it, every weapon whose own perks give it.
    /// </summary>
    public static ImmutableArray<int> ListWeaponsShowingBuff(ValidatedBuild build, StatusId buff, Optional<ElementId> element) =>
        element.Map(shown => BuildValidation.ListWeaponsHolding(build.Catalog, build.Build, shown)).UnwrapOr([]) is { IsEmpty: false } holding
            ? holding
            : BuildValidation.ListWeaponsGiving(build.Catalog, build.Build, buff);

    /// <summary>The static view of a build: every trigger and what it sets off, as a tree, the sources aligned.</summary>
    public static ImmutableArray<StyledLine> ExplainBuild(ValidatedBuild build) =>
        BuildExplaining.RenderExplanation(build, BuildExplaining.ExplainBuild(build));

    /// <summary>The build by outcome: everything it can get you, each with every way the build gets it, ends it or changes it.</summary>
    public static ImmutableArray<StyledLine> ExplainOutcomes(ValidatedBuild build) =>
        OutcomeExplaining.RenderOutcomes(build, OutcomeExplaining.ExplainOutcomes(build));

    /// <summary>The report as styled lines: the order, the verdict, each step's needs, what it sets off, what is wasted.</summary>
    public static ImmutableArray<StyledLine> RenderLoopReport(LoopReport report) =>
        LoopReportRendering.RenderLoopReport(report);
}

/// <summary>Parse the rule catalog once (the web host reads the rule files from embedded resources).</summary>
public static class CatalogLoading
{
    public static Result<RuleCatalog, string> ParseCatalog(ImmutableArray<SourceText> ruleFiles) =>
        RuleCatalogParsing.ParseCatalog(ruleFiles);
}
