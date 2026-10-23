using System.Collections.Immutable;
using System.Globalization;
using Loopsmith.Core.Domain;
using Loopsmith.Functional;

namespace Loopsmith.Core.Phrasing;

/// <summary>Kernel: plain-English descriptions of Domain values, shared by every renderer.</summary>
public static class DomainPhrasing
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>What is shown for something not known: a "?" number, an ability Loopsmith can't name.</summary>
    public const string Unknown = "?";

    // ── numbers ────────────────────────────────────────────────────────────────

    public static string FormatPercent(this GameValue value) =>
        value.Match(
            known => FormatPercentNumber(known.Value),
            perModCount => string.Join("|", perModCount.Values.Select(FormatPercentNumber)),
            approximate => "~" + FormatPercentNumber(approximate.Value),
            _ => "?%");

    /// <summary>A bonus: "+50%", "+10%|+17%", "~+25%"; one whose amount isn't known, "?" (never 0).</summary>
    public static string FormatSignedPercent(this GameValue value) =>
        value.Match(
            known => "+" + FormatPercentNumber(known.Value),
            perModCount => string.Join("|", perModCount.Values.Select(fraction => "+" + FormatPercentNumber(fraction))),
            approximate => "~+" + FormatPercentNumber(approximate.Value),
            _ => Unknown);

    public static string FormatNumber(this GameValue value) =>
        value.Match(
            known => known.Value.ToString("0.##", Invariant),
            perModCount => string.Join("|", perModCount.Values.Select(v => v.ToString("0.##", Invariant))),
            approximate => "~" + approximate.Value.ToString("0.##", Invariant),
            _ => Unknown);

    public static string FormatSeconds(this Seconds seconds) =>
        seconds.Value.ToString("0.#", Invariant) + "s";

    private static string FormatPercentNumber(decimal fraction) =>
        (fraction * 100m).ToString("0.##", Invariant) + "%";

    // ── names ──────────────────────────────────────────────────────────────────

    /// <summary>"Kinetic", "Energy", "Heavy": a weapon slot as the game names it.</summary>
    public static string DescribeWeaponSlot(this WeaponSlot slot) =>
        slot switch
        {
            WeaponSlot.Kinetic => "Kinetic",
            WeaponSlot.Energy => "Energy",
            _ => "Heavy",
        };

    /// <summary>"Helmet", "Arms", "Chest", "Legs", "Class item".</summary>
    /// <summary>
    /// What a mod socket takes, in short (an empty socket's square): its slot ("Helmet"), "General", "Artifice", else
    /// "Special" (a socket the designer doesn't list: a raid's or an activity's, ADRs D35).
    /// </summary>
    public static string DescribeModSocketShort(this PlugCategoryId category) =>
        ArmorModCategories.FindSlot(category).Match(
            slot => slot.Value.DescribeArmorSlot(),
            _ => category == ArmorModCategories.General ? "General"
                : category == ArmorModCategories.Artifice ? "Artifice"
                : "Special");

    /// <summary>What a mod socket takes, as the game words its mods ("Helmet mod", "General mod", "Artifice mod"); none for another (its mods' type says).</summary>
    public static Optional<string> DescribeModSocket(this PlugCategoryId category) =>
        ArmorModCategories.FindSlot(category).Match(
            slot => Optional.Some($"{slot.Value.DescribeArmorSlot()} mod"),
            _ => category == ArmorModCategories.General ? Optional.Some("General mod")
                : category == ArmorModCategories.Artifice ? Optional.Some("Artifice mod")
                : Optional.None<string>());

    public static string DescribeArmorSlot(this ArmorSlot slot) =>
        slot switch
        {
            ArmorSlot.Helmet => "Helmet",
            ArmorSlot.Arms => "Arms",
            ArmorSlot.Chest => "Chest",
            ArmorSlot.Legs => "Legs",
            _ => "Class item",
        };

    /// <summary>"grenade-launcher" → "Grenade Launcher": a weapon archetype as a label.</summary>
    public static string DescribeArchetype(string archetype) =>
        ToTitleWords(archetype);

    /// <summary>"heat-weapon" → "Heat Weapon", "reload" → "Reload": a rule's id as words.</summary>
    private static string ToTitleWords(string id) =>
        string.Join(' ', id.Split('-', StringSplitOptions.RemoveEmptyEntries).Select(word => char.ToUpperInvariant(word[0]) + word[1..]));

    /// <summary>A hash by its manifest name, or the number when the excerpt doesn't have it.</summary>
    public static string DescribeItemHash(this ManifestExcerpt manifest, ItemHash hash) =>
        manifest.FindItem(hash).Match(item => item.Value.Name, _ => hash.Value.ToString(Invariant));

    /// <summary>
    /// A hash by its manifest name and type as the game words it ("Slice (Enhanced Trait)": a perk and its enhanced
    /// version share a name), or the number when the excerpt doesn't have it.
    /// </summary>
    public static string DescribeTypedItemHash(this ManifestExcerpt manifest, ItemHash hash) =>
        manifest.FindItem(hash).Match(item => item.Value.Type.Match(type => $"{item.Value.Name} ({type.Value})", _ => item.Value.Name), _ => hash.Value.ToString(Invariant));

    /// <summary>Where an element's facts come from, as a heading ("Destiny Data Compendium", "A creator's claim").</summary>
    public static string DescribeProvenanceTitle(this Provenance source) =>
        source.Match(
            _ => "Destiny Data Compendium",
            _ => "Clarity",
            _ => "Loopsmith's own rule",
            _ => "A creator's claim");

    /// <summary>Which row or entry of it ("Armor Perks tab, row 57 · snapshot of 2026-10-09"; a rule file's line).</summary>
    public static string DescribeProvenanceCite(this Provenance source) =>
        source.Match(
            compendium => $"{compendium.Tab} tab, row {compendium.Row.ToString(Invariant)} · snapshot of {compendium.Snapshot.Value.ToString("yyyy-MM-dd", Invariant)}",
            clarity => $"Clarity {clarity.ClarityVersion} · item {clarity.Hash.Value.ToString(Invariant)}",
            authored => $"{authored.RuleFile}, line {authored.Line.ToString(Invariant)} · no Compendium or Clarity row cited",
            _ => "Not checked against the game data (Compendium or Clarity) yet");

    /// <summary>What kind of element it is, as a label ("Aspect", "Exotic weapon perk", "Armor set bonus").</summary>
    public static string DescribeElementKind(this ElementKind kind) =>
        kind switch
        {
            ElementKind.Keyword => "Keyword",
            ElementKind.Super => "Super",
            ElementKind.Grenade => "Grenade",
            ElementKind.Melee => "Melee",
            ElementKind.ClassAbility => "Class ability",
            ElementKind.Jump => "Jump",
            ElementKind.Aspect => "Aspect",
            ElementKind.Fragment => "Fragment",
            ElementKind.ExoticArmor => "Exotic armor",
            ElementKind.ExoticWeapon => "Exotic weapon perk",
            ElementKind.Catalyst => "Catalyst",
            ElementKind.ArmorSetBonus => "Armor set bonus",
            ElementKind.ArmorMod => "Armor mod",
            ElementKind.ArtifactPerk => "Artifact perk",
            ElementKind.WeaponMod => "Weapon mod",
            _ => "Weapon perk",
        };

    public static string DescribeLoadoutPart(this LoadoutPart part) =>
        part switch
        {
            LoadoutPart.Item => "item",
            LoadoutPart.SubclassPlug => "subclass plug",
            LoadoutPart.ArmorMod => "armor mod",
            _ => "artifact perk",
        };

    /// <summary>The heading for a part's left-out hashes: "Items", "Subclass plugs", "Armor mods", "Artifact perks".</summary>
    public static string DescribeLoadoutParts(this LoadoutPart part) =>
        part switch
        {
            LoadoutPart.Item => "Items",
            LoadoutPart.SubclassPlug => "Subclass plugs",
            LoadoutPart.ArmorMod => "Armor mods",
            _ => "Artifact perks",
        };

    /// <summary>"3 items, 1 subclass plug, 2 armor mods": what a build has that its rules leave out.</summary>
    public static string DescribeLeftOut(this ImmutableArray<LeftOutItem> leftOut) =>
        string.Join(", ", leftOut
            .GroupBy(item => item.Part)
            .OrderBy(group => group.Key)
            .Select(group => $"{group.Count()} {group.Key.DescribeLoadoutPart()}{(group.Count() == 1 ? "" : "s")}"));

    /// <summary>
    /// An ability's energy (ADRs D48), to two decimals at most: "100%", "8.44%", "2 charges", "1 charge + 8.44%"; part of it
    /// unknown: "≥ 8.44%", and all of it ("?"); approximate: "~40%". Never 100% while a charge isn't full.
    /// </summary>
    public static string DescribeEnergy(AbilityEnergy energy, ChargeCount charges)
    {
        var total = charges.Value;
        var full = energy.Charges.Value >= total;
        var text = (total, full, energy.Charges.Value, energy.Progress.Value) switch
        {
            (1, true, _, _) => "100%",
            (_, true, _, _) => $"{total} charges",
            (_, false, 0, var progress) => FormatEnergyShare(progress),
            (_, false, var count, 0m) => DescribeCharges(count),
            (_, false, var count, var progress) => $"{DescribeCharges(count)} + {FormatEnergyShare(progress)}",
        };
        return (full, energy.Certainty, energy.ReadTotal()) switch
        {
            (true, _, _) => text,
            (false, EnergyCertainty.AtLeast, 0m) => "?",
            (false, EnergyCertainty.AtLeast, _) => $"≥ {text}",
            (false, EnergyCertainty.Approximate, _) => $"~{text}",
            _ => text,
        };
    }

    /// <summary>
    /// "Grenade not ready: 0% before #2", "Grenade may not be ready: ≥ 8.44% before #2" (ADRs D48); none when the step's
    /// ability was ready.
    /// </summary>
    public static Optional<ReadinessFlag> DescribeReadiness(AbilityReadiness readiness, ChargeCount charges, int step) =>
        readiness.Readiness switch
        {
            Readiness.NotReady => Optional.Some<ReadinessFlag>(new ReadinessFlag.NotReady($"{Capitalize(readiness.Ability.DescribeAbility())} not ready: {DescribeEnergy(readiness.Before, charges)} before #{step}")),
            Readiness.MayNotBeReady => Optional.Some<ReadinessFlag>(new ReadinessFlag.MayNotBeReady($"{Capitalize(readiness.Ability.DescribeAbility())} may not be ready: {DescribeEnergy(readiness.Before, charges)} before #{step}")),
            _ => Optional.None<ReadinessFlag>(),
        };

    /// <summary>The note of a step whose ability wasn't ready: "Grenade not ready: 0% before #2".</summary>
    public static string ReadNote(this ReadinessFlag flag) =>
        flag.Match(notReady => notReady.Note, mayNotBeReady => mayNotBeReady.Note);

    /// <summary>The badge in the step's header: "Not ready", "May not be ready".</summary>
    public static string DescribeReadinessBadge(this ReadinessFlag flag) =>
        flag.Match(_ => "Not ready", _ => "May not be ready");

    /// <summary>
    /// Where a least-charged grant went (ADRs D48), as its caveat: "to your grenade"; "to your grenade or melee: which is
    /// least charged is unknown"; "every ability is full: maybe to your Super".
    /// </summary>
    public static string DescribeLeastCharged(LeastCharged least) =>
        least.Match(
            sure => $"to your {sure.Ability.DescribeAbility()}",
            _ => "every ability is full: maybe to your Super",
            open => $"to your {string.Join(" or ", open.Candidates.Select(ability => ability.DescribeAbility()))}: which is least charged is unknown");

    private static string DescribeCharges(int count) =>
        count == 1 ? "1 charge" : $"{count} charges";

    /// <summary>A share of one charge as a percentage, to two decimals at most, never rounded up to 100% (8.4375% → "8.44%").</summary>
    public static string FormatEnergyShare(decimal share)
    {
        var rounded = Math.Round(share * 100m, 2, MidpointRounding.AwayFromZero);
        var shown = rounded >= 100m && share < 1m ? 99.99m : rounded;
        return $"{shown.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}%";
    }

    /// <summary>A stat as the game names it: "Melee", "Class", "Weapons".</summary>
    public static string DescribeStat(this StatKind stat) =>
        stat.ToString();

    public static string DescribeAbility(this AbilityKind kind) =>
        kind switch
        {
            AbilityKind.Grenade => "grenade",
            AbilityKind.Melee => "melee",
            AbilityKind.ClassAbility => "class ability",
            _ => "super",
        };

    /// <summary>
    /// An ability as it was used: its plain name for a press, else its secondary ("sliding melee", "airborne melee",
    /// "class ability in the air", "held grenade").
    /// </summary>
    public static string DescribeAbilityUse(this AbilityKind kind, AbilityInput input) =>
        (kind, input) switch
        {
            (_, AbilityInput.Press) => kind.DescribeAbility(),
            (AbilityKind.Melee, AbilityInput.Slide) => "sliding melee",
            (AbilityKind.Melee, AbilityInput.Air) => "airborne melee",
            (AbilityKind.ClassAbility, AbilityInput.Air) => "class ability in the air",
            (AbilityKind.Super, AbilityInput.Air) => "airborne Super heavy",
            (AbilityKind.Super, AbilityInput.Combo) => "Super heavy combo",
            (AbilityKind.Super, AbilityInput.Guard) => "Super guard",
            (_, AbilityInput.Hold) => $"held {kind.DescribeAbility()}",
            _ => $"{kind.DescribeAbility()} ({ToInputWord(input)})",
        };

    /// <summary>
    /// How the player does an ability's use: "melee" for a press, else "slide + melee", "melee in the air", "air move" (the
    /// air move key in the air, which spends the class ability's charge: Ascension), "hold grenade".
    /// </summary>
    public static string DescribeGesture(this AbilityKind kind, AbilityInput input) =>
        (kind, input) switch
        {
            (_, AbilityInput.Slide) => $"slide + {kind.DescribeAbility()}",
            (AbilityKind.ClassAbility, AbilityInput.Air) => "air move",
            (AbilityKind.Super, AbilityInput.Air) => "heavy attack in the air",
            (AbilityKind.Super, AbilityInput.Combo) => "light, light, heavy",
            (AbilityKind.Super, AbilityInput.Guard) => "hold guard",
            (_, AbilityInput.Air) => $"{kind.DescribeAbility()} in the air",
            (_, AbilityInput.Hold) => $"hold {kind.DescribeAbility()}",
            _ => kind.DescribeAbility(),
        };

    /// <summary>
    /// The input's word in an action token and a rule (<c>melee:slide</c>, <c>class:air</c>, <c>grenade:hold</c>); a press is
    /// <c>press</c> in a rule (<c>input: press</c>), and an action token writes none (<see cref="ToInputSuffix"/>).
    /// </summary>
    public static string ToInputWord(this AbilityInput input) =>
        input switch
        {
            AbilityInput.Slide => "slide",
            AbilityInput.Air => "air",
            AbilityInput.Hold => "hold",
            AbilityInput.Combo => "combo",
            AbilityInput.Guard => "guard",
            _ => "press",
        };

    public static string DescribeStatus(this KeywordGlossary glossary, StatusId status) =>
        glossary.Statuses.TryGetValue(status, out var definition) ? definition.Name : status.Value;

    public static string DescribePickup(this KeywordGlossary glossary, PickupId pickup) =>
        glossary.Pickups.TryGetValue(pickup, out var definition) ? definition.Name : pickup.Value;

    public static string DescribeSummon(this KeywordGlossary glossary, SummonId summon) =>
        glossary.Summons.TryGetValue(summon, out var definition) ? definition.Name : summon.Value;

    /// <summary>
    /// A phrase in parts: each keyword the glossary knows — a status by its name or, for a debuff, its adjective
    /// ("Jolted"), a pickup, a summon — apart, as a whole word (the longest name first: "Unraveling Rounds" before
    /// "Unravel"), the text between plain. Hosts highlight the keywords.
    /// </summary>
    public static ImmutableArray<PhrasePart> SplitKeywords(this KeywordGlossary glossary, string text)
    {
        var names = ListKeywordNames(glossary);
        var parts = ImmutableArray.CreateBuilder<PhrasePart>();
        var plainStart = 0;
        var at = 0;
        while (at < text.Length)
        {
            if (FindKeywordAt(names, text, at) is not Optional<(string Text, KeywordName Keyword)>.Some match)
            {
                at++;
                continue;
            }

            if (at > plainStart)
            {
                parts.Add(new PhrasePart(text[plainStart..at], Optional.None<KeywordName>()));
            }

            parts.Add(new PhrasePart(match.Value.Text, Optional.Some(match.Value.Keyword)));
            at += match.Value.Text.Length;
            plainStart = at;
        }

        if (text.Length > plainStart)
        {
            parts.Add(new PhrasePart(text[plainStart..], Optional.None<KeywordName>()));
        }

        return parts.ToImmutable();
    }

    /// <summary>The keyword whose name starts at this place as a whole word (the longest first), when one does.</summary>
    private static Optional<(string Text, KeywordName Keyword)> FindKeywordAt(ImmutableArray<(string Text, KeywordName Keyword)> names, string text, int at) =>
        names.Where(name => IsWordAt(text, at, name.Text)).Take(1).Select(name => Optional.Some(name)).FindFirstSome();

    private static ImmutableArray<(string Text, KeywordName Keyword)> ListKeywordNames(KeywordGlossary glossary) =>
    [
        .. glossary.Statuses.Values
            .SelectMany(status => new[] { status.Name, status.Kind == KeywordKind.Debuff ? glossary.DescribeDebuffedAdjective(status.Id) : status.Name }
                .Distinct()
                .Select(name => (Text: name, Keyword: new KeywordName(new KeywordRef.Status(status.Id), status.Affinity))))
            .Concat(glossary.Pickups.Values.Select(pickup => (Text: pickup.Name, Keyword: new KeywordName(new KeywordRef.Pickup(pickup.Id), pickup.Affinity))))
            .Concat(glossary.Summons.Values.Select(summon => (Text: summon.Name, Keyword: new KeywordName(new KeywordRef.Summon(summon.Id), summon.DamageType.ToAffinity()))))
            .Where(name => name.Text.Length > 0)
            .OrderByDescending(name => name.Text.Length)
            .ThenBy(name => name.Text, StringComparer.Ordinal),
    ];

    private static bool IsWordAt(string text, int at, string word) =>
        string.CompareOrdinal(text, at, word, 0, word.Length) == 0
        && (at == 0 || !char.IsLetterOrDigit(text[at - 1]))
        && (at + word.Length == text.Length || !char.IsLetterOrDigit(text[at + word.Length]));

    /// <summary>
    /// "Jolt" → "Jolted", "Sever" → "Severed" — the adjective a player uses for a debuffed target: the glossary's
    /// <c>adjective</c> when it has one ("Freeze" → "Frozen"), else the name with "ed" (or "d") added.
    /// </summary>
    public static string DescribeDebuffedAdjective(this KeywordGlossary glossary, StatusId status) =>
        glossary.Statuses.TryGetValue(status, out var definition)
            ? definition.Adjective.UnwrapOr(AddPastSuffix(definition.Name))
            : AddPastSuffix(glossary.DescribeStatus(status));

    private static string AddPastSuffix(string name) =>
        name switch
        {
            _ when name.Contains(' ') => name,
            _ when name.EndsWith('e') => name + "d",
            _ when name.EndsWith("ed", StringComparison.Ordinal) => name,
            _ => name + "ed",
        };

    public static Affinity ReadStatusAffinity(this KeywordGlossary glossary, StatusId status) =>
        glossary.Statuses.TryGetValue(status, out var definition) ? definition.Affinity : Affinity.Neutral;

    // ── damage ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// "Arc weapon", "grenade", "Jolt"… <paramref name="superType"/> is the build's Super's damage type
    /// (<c>BuildReading.ReadSuperType</c>): a weapon of your Super's element names it when it is known ("Arc weapon (your
    /// Super's element)"), else "weapon of your Super's element".
    /// </summary>
    public static string DescribeDamageSource(this KeywordGlossary glossary, DamageSource source, Optional<DamageType> superType) =>
        source.Match(
            _ => "any source",
            _ => "weapon",
            weaponOfType => $"{weaponOfType.Type} weapon",
            weaponInSlot => $"{weaponInSlot.Slot.DescribeWeaponSlot().ToLowerInvariant()} weapon",
            _ => "ability",
            abilityOf => abilityOf.Kind.DescribeAbility(),
            ofType => $"{ofType.Type}",
            keywordOf => glossary.DescribeStatus(keywordOf.Status),
            summonOf => glossary.DescribeSummon(summonOf.Summon),
            _ => superType.Match(type => $"{type.Value} weapon (your Super's element)", _ => "weapon of your Super's element"),
            usedAs => usedAs.Kind.DescribeAbilityUse(usedAs.Input),
            _ => "this weapon",
            thisOfType => $"this weapon ({thisOfType.Type})",
            ofAmmo => $"{ofAmmo.Ammo} weapon",
            ofAmmoAndType => $"{ofAmmoAndType.Ammo} {ofAmmoAndType.Type} weapon",
            ofArchetype => DescribeArchetype(ofArchetype.Archetype));

    public static string DescribeOrigin(this KeywordGlossary glossary, DamageOrigin origin, Build build) =>
        origin.Match(
            weapon => DescribeWeapon(build, weapon.Slot, $"{weapon.Type} weapon"),
            ability => Capitalize(ability.Kind.DescribeAbilityUse(ability.Input)),
            keyword => glossary.DescribeStatus(keyword.Status),
            summoned => glossary.DescribeSummon(summoned.Summon),
            _ => Finisher);

    /// <summary>What a finisher is called everywhere: the action, its trigger, its event and its hit.</summary>
    public const string Finisher = "Finisher";

    /// <summary>What reaching critical health is called everywhere: the declared step, its trigger and its event.</summary>
    public const string CriticalHealth = "Critical health";

    /// <summary>What being damaged while at critical health is called everywhere: the declared step, its trigger and its event.</summary>
    public const string HitAtCriticalHealth = "Hit at critical health";

    /// <summary>Its button, beside "Critical health" among the buffs on you: "hit".</summary>
    public const string HitAtCriticalHealthCaption = "hit";

    /// <summary>What reloading a weapon is called: the step ("Reload Festival Flight"), its trigger ("Reload this weapon") and its button (ADRs D37).</summary>
    public const string Reload = "Reload";

    /// <summary>A Reload step's button, next to a weapon's hit and kill: "🔄 reload".</summary>
    public const string ReloadCaption = "🔄 reload";

    /// <summary>The designer's square for picking up an ammo brick (ADRs D43), its row's title: "Ammo brick", after its icon, "📦".</summary>
    public const string AmmoBrick = "Ammo brick";

    /// <summary>The ammo brick square's icon: "📦".</summary>
    public const string AmmoBrickIcon = "📦";

    /// <summary>The ammo brick step's option that the player slides over it, in its row: "🛝 by sliding".</summary>
    public const string SlideOption = "🛝 by sliding";

    /// <summary>
    /// An ammo brick picked up, as a step and as a trigger (ADRs D43): "Pick up a Heavy ammo brick", "Slide over a Special ammo
    /// brick"; with no brick named (a trigger on any), "Pick up an ammo brick", "Slide over an ammo brick".
    /// </summary>
    public static string DescribeAmmoPickup(Optional<AmmoBrick> brick, BrickPickup pickup)
    {
        var verb = pickup == BrickPickup.Slide ? "Slide over" : "Pick up";
        var what = brick.Match(some => $"a {some.Value} ammo brick", _ => "an ammo brick");
        return $"{verb} {what}";
    }

    /// <summary>What readying (swapping to) a weapon is called: the step ("Ready Buzzard") and its trigger (ADRs D44).</summary>
    public const string ReadyWeapon = "Ready";

    /// <summary>A Ready step's button, next to a weapon's hit and kill: "✋ ready".</summary>
    public const string ReadyWeaponCaption = "✋ ready";

    /// <summary>What breaking a combatant's shield is called as a trigger (ADRs D36).</summary>
    public const string BreakShield = "Break a shield";

    /// <summary>A weapon step's option that it breaks a shield, in its label and its button: "🛡 shield".</summary>
    public const string ShieldOption = "🛡 shield";

    /// <summary>A weapon step's option that its hits and kills are precision ones (headshots), in its label and its button: "🎯 precision".</summary>
    public const string PrecisionOption = "🎯 precision";

    /// <summary>An ability step's option that it spends the last charge (its energy fully expended), in its label: "⬜ spend fully".</summary>
    public const string LastChargeOption = "⬜ spend fully";

    /// <summary>The same option on its box in the designer: "⬜ Spend fully".</summary>
    public const string LastChargeBox = "⬜ Spend fully";

    /// <summary>The designer's picker of the rank of the enemy a kill step kills (ADRs D45): "Rank".</summary>
    public const string RankPicker = "Rank";

    /// <summary>An enemy's rank as the designer and a step's label say it: "Minor", "Elite", "Miniboss", "Champion", "Boss".</summary>
    public static string DescribeRank(this EnemyTier rank) =>
        rank.ToString();

    /// <summary>A kill trigger's rank: "boss", or "elite+" for an elite or stronger.</summary>
    public static string DescribeTierNeed(TierNeed tier) =>
        tier.Match(
            exactly => exactly.Tier.ToString().ToLowerInvariant(),
            atLeast => atLeast.Tier.ToString().ToLowerInvariant() + "+");

    /// <summary>" · Elite" in a kill step's label when the player says it kills a stronger enemy than the pack's minors; nothing for a minor.</summary>
    private static string DescribeRankOption(EnemyTier rank) =>
        rank == EnemyTier.Minor ? "" : $" · {rank.DescribeRank()}";

    // ── triggers, conditions, outcomes, passives ───────────────────────────────

    /// <summary>A rule's trigger as a player says it; <paramref name="superType"/>: see <see cref="DescribeDamageSource"/>.</summary>
    public static string DescribeTrigger(this KeywordGlossary glossary, Trigger trigger, Optional<DamageType> superType) =>
        trigger.Match(
            cast => DescribeCast(cast.Kind),
            killAny => DescribeKill(glossary, killAny.Via, [], superType) + DescribeZone(killAny.Needs),
            killOfTier => $"{DescribeKill(glossary, killOfTier.Via, [], superType)} ({DescribeTierNeed(killOfTier.Tier)})" + DescribeZone(killOfTier.Needs),
            killDebuffed => DescribeKill(glossary, killDebuffed.Via, killDebuffed.TargetHas, superType) + DescribeZone(killDebuffed.Needs),
            killMultiple => DescribeMultiKill(glossary, killMultiple.Via, $"{killMultiple.AtLeast.Value.ToString(Invariant)}+", superType)
                + DescribeWithin(killMultiple.Within) + DescribeZone(killMultiple.Needs),
            damage => DescribeDamage(glossary, damage.Via, [], superType) + DescribeSpan(damage.Needs),
            damageDebuffed => DescribeDamage(glossary, damageDebuffed.Via, damageDebuffed.TargetHas, superType) + DescribeSpan(damageDebuffed.Needs),
            damageMultiple => DescribeMultiHit(glossary, damageMultiple.Via, damageMultiple.AtLeast, superType) + DescribeWithin(damageMultiple.Within),
            pickUp => $"Pick up {glossary.DescribePickup(pickUp.Pickup)}",
            buffGained => $"Gain {glossary.DescribeStatus(buffGained.Status)}",
            stacksMaxed => $"Max {glossary.DescribeStatus(stacksMaxed.Status)}",
            _ => Finisher,
            castAs => DescribeCast(castAs.Kind, castAs.Input),
            killFewer => DescribeFewKills(glossary, killFewer.Via, killFewer.FewerThan, superType) + DescribeZone(killFewer.Needs),
            _ => CriticalHealth,
            shieldBroken => shieldBroken.Via is DamageSource.AnySource
                ? BreakShield
                : $"{BreakShield} with {glossary.DescribeDamageSource(shieldBroken.Via, superType)}",
            energyExpended => DescribeEnergyExpended(energyExpended.Kind),
            damageFewer => DescribeFewHits(glossary, damageFewer.Via, damageFewer.FewerThan, superType),
            killBetween => DescribeMultiKill(glossary, killBetween.Via, DescribeKillRange(killBetween.AtLeast, killBetween.FewerThan), superType)
                + DescribeZone(killBetween.Needs),
            reload => reload.Via is DamageSource.AnySource or DamageSource.AnyWeapon
                ? Reload
                : $"{Reload} {glossary.DescribeDamageSource(reload.Via, superType)}",
            _ => HitAtCriticalHealth,
            ammo => DescribeAmmoPickup(ammo.Brick, ammo.Needs == BrickNeed.Slide ? BrickPickup.Slide : BrickPickup.Plain),
            ready => ready.Via is DamageSource.AnySource or DamageSource.AnyWeapon
                ? $"{ReadyWeapon} a weapon"
                : $"{ReadyWeapon} {glossary.DescribeDamageSource(ready.Via, superType)}");

    /// <summary>" in 3s": the window a source gives the kills or hits it counts; nothing without one.</summary>
    private static string DescribeWithin(Optional<Seconds> within) =>
        within.Match(some => $" in {some.Value.FormatSeconds()}", _ => "");

    /// <summary>"Grenade energy fully expended": the ability's last charge used (ADRs D36), as Bungie's text of the Kickstarts says it.</summary>
    public static string DescribeEnergyExpended(this AbilityKind kind) =>
        $"{Capitalize(kind.DescribeAbility())} energy fully expended";

    private static string DescribeKill(KeywordGlossary glossary, DamageSource via, ImmutableArray<StatusId> targetHas, Optional<DamageType> superType)
    {
        var target = DescribeTargetPhrase(glossary, targetHas);
        return via switch
        {
            DamageSource.AnySource => $"Kill {target}",
            DamageSource.AbilityOf ability => $"{Capitalize(ability.Kind.DescribeAbility())} kill{DescribeOnTarget(target)}",
            DamageSource.AbilityUsedAs usedAs => $"{Capitalize(usedAs.Kind.DescribeAbilityUse(usedAs.Input))} kill{DescribeOnTarget(target)}",
            DamageSource.OfType type => $"{type.Type} kill{DescribeOnTarget(target)}",
            DamageSource.KeywordOf keyword => $"{glossary.DescribeStatus(keyword.Status)} kill{DescribeOnTarget(target)}",
            _ => $"Kill {target} with {glossary.DescribeDamageSource(via, superType)}",
        };
    }

    /// <summary>" (precision)", " (body)": where a source needs its kill to land, which the player says (ADRs D36).</summary>
    private static string DescribeZone(ZoneNeed needs) =>
        needs.Match(
            _ => "",
            _ => " (body)",
            _ => " (precision)");

    /// <summary>" kept up (a hit step)": damage a source needs over several hits, which a hit step stands for (ADRs D34).</summary>
    private static string DescribeSpan(DamageSpan needs) =>
        needs == DamageSpan.Sustained ? " kept up (a hit step)" : "";

    /// <summary>"Arc weapon damage", "Damage target with Arc weapon (your Super's element)": a long source reads after the target.</summary>
    private static string DescribeDamage(KeywordGlossary glossary, DamageSource via, ImmutableArray<StatusId> targetHas, Optional<DamageType> superType)
    {
        var target = DescribeTargetPhrase(glossary, targetHas);
        return via switch
        {
            DamageSource.AnySource => $"Damage {target}",
            DamageSource.WeaponOfSuperType => $"Damage {target} with {glossary.DescribeDamageSource(via, superType)}",
            _ => $"{Capitalize(glossary.DescribeDamageSource(via, superType))} damage{DescribeOnTarget(target)}",
        };
    }

    /// <summary>"Kill 2+ with grenade", "Kill 3+ enemies" (any source).</summary>
    /// <summary>"Kill 2+ with weapon", "Kill 2 enemies" (any source): <paramref name="count"/> is "2+", "2" or "2–3".</summary>
    private static string DescribeMultiKill(KeywordGlossary glossary, DamageSource via, string count, Optional<DamageType> superType) =>
        via is DamageSource.AnySource
            ? $"Kill {count} enemies"
            : $"Kill {count} with {glossary.DescribeDamageSource(via, superType)}";

    /// <summary>"2" (at least 2, fewer than 3), "2–3" (at least 2, fewer than 4).</summary>
    private static string DescribeKillRange(TargetCount atLeast, TargetCount fewerThan) =>
        fewerThan.Value - 1 == atLeast.Value
            ? atLeast.Value.ToString(Invariant)
            : $"{atLeast.Value.ToString(Invariant)}–{(fewerThan.Value - 1).ToString(Invariant)}";

    /// <summary>"Kill 1–11 with this weapon", "Kill 1–11 enemies" (any source): fewer than N in one action, at least one.</summary>
    private static string DescribeFewKills(KeywordGlossary glossary, DamageSource via, TargetCount fewerThan, Optional<DamageType> superType)
    {
        var most = fewerThan.Value - 1;
        var range = most == 1 ? "1" : $"1–{most.ToString(Invariant)}";
        return via is DamageSource.AnySource
            ? $"Kill {range} {(most == 1 ? "enemy" : "enemies")}"
            : $"Kill {range} with {glossary.DescribeDamageSource(via, superType)}";
    }

    /// <summary>"Hit 1–2 enemies with this weapon", "Hit 1 enemy" (any source): fewer than N in one action, at least one.</summary>
    private static string DescribeFewHits(KeywordGlossary glossary, DamageSource via, TargetCount fewerThan, Optional<DamageType> superType)
    {
        var most = fewerThan.Value - 1;
        var range = most == 1 ? "1" : $"1–{most.ToString(Invariant)}";
        var enemies = most == 1 ? "enemy" : "enemies";
        return via is DamageSource.AnySource
            ? $"Hit {range} {enemies}"
            : $"Hit {range} {enemies} with {glossary.DescribeDamageSource(via, superType)}";
    }

    /// <summary>"Hit 3+ enemies with weapon", "Hit 3+ enemies" (any source).</summary>
    private static string DescribeMultiHit(KeywordGlossary glossary, DamageSource via, TargetCount atLeast, Optional<DamageType> superType) =>
        via is DamageSource.AnySource
            ? $"Hit {atLeast.Value}+ enemies"
            : $"Hit {atLeast.Value}+ enemies with {glossary.DescribeDamageSource(via, superType)}";

    /// <summary>
    /// "+20% Arc weapon damage", "+100% [PvP 10%] Sidearm damage": with its PvP value when the source gives one; a weapon of
    /// your Super's element reads "+20% damage with Arc weapon (your Super's element)".
    /// </summary>
    private static string DescribeDamageChange(KeywordGlossary glossary, DamageSource against, GameValue change, Optional<GameValue> pvp, Optional<DamageType> superType) =>
        against is DamageSource.WeaponOfSuperType
            ? $"+{change.FormatPercent()}{DescribePvp(pvp)} damage with {glossary.DescribeDamageSource(against, superType)}"
            : $"+{change.FormatPercent()}{DescribePvp(pvp)} {glossary.DescribeDamageSource(against, superType)} damage";

    /// <summary>" [PvP 10%]": a value's PvP part, when the source gives one; nothing without.</summary>
    public static string DescribePvp(Optional<GameValue> pvp) =>
        pvp.Map(value => $" [PvP {value.FormatPercent()}]").UnwrapOr("");

    private static string DescribeTargetPhrase(KeywordGlossary glossary, ImmutableArray<StatusId> targetHas) =>
        targetHas.IsEmpty
            ? "target"
            : string.Join(" + ", targetHas.Select(glossary.DescribeDebuffedAdjective)) + " target";

    /// <summary>The ability-cast trigger as a player says it ("Grenade thrown", "Melee swung while sliding", "Grenade held").</summary>
    public static string DescribeCast(AbilityKind kind, AbilityInput input) =>
        (kind, input) switch
        {
            (_, AbilityInput.Press) => DescribeCast(kind),
            (AbilityKind.ClassAbility, AbilityInput.Air) => "Class ability in the air",
            (AbilityKind.Melee, AbilityInput.Slide) => "Melee swung while sliding",
            (AbilityKind.Melee, AbilityInput.Air) => "Melee swung in the air",
            (AbilityKind.Grenade, AbilityInput.Hold) => "Grenade held",
            (AbilityKind.Super, AbilityInput.Air) => "Super heavy attack in the air",
            (AbilityKind.Super, AbilityInput.Combo) => "Super heavy combo",
            (AbilityKind.Super, AbilityInput.Guard) => "Super guard",
            _ => $"{Capitalize(kind.DescribeAbilityUse(input))} used",
        };

    private static string DescribeCast(AbilityKind kind) =>
        kind switch
        {
            AbilityKind.Grenade => "Grenade thrown",
            AbilityKind.Melee => "Melee swung",
            AbilityKind.ClassAbility => "Class ability",
            _ => "Super cast",
        };

    private static string DescribeOnTarget(string target) => target == "target" ? "" : $" on {target}";

    private static string DescribeWeapon(Build build, WeaponSlot slot, string fallback) =>
        build.Weapons
            .Select(w => w.Slot == slot ? Optional.Some(w.Name) : Optional.None<string>())
            .FindFirstSome()
            .UnwrapOr(fallback);

    public static string DescribeCondition(this KeywordGlossary glossary, Condition condition) =>
        condition.Match(
            hasBuff => $"while {glossary.DescribeStatus(hasBuff.Status)}",
            lacksBuff => $"without {glossary.DescribeStatus(lacksBuff.Status)}",
            targetHas => $"if target {glossary.DescribeDebuffedAdjective(targetHas.Status)}",
            atMax => $"while {glossary.DescribeStatus(atMax.Status)} at max",
            during => $"during your {during.Ability.DescribeAbility()}",
            belowMax => $"while {glossary.DescribeStatus(belowMax.Status)} below max",
            holds => $"with x{holds.AtLeast.Value} or more {glossary.DescribeStatus(holds.Status)}");

    public static string DescribeConditions(this KeywordGlossary glossary, ImmutableArray<Condition> conditions) =>
        string.Join(", ", conditions.Select(glossary.DescribeCondition));

    /// <summary>An outcome as a fact; <paramref name="superType"/>: see <see cref="DescribeDamageSource"/>.</summary>
    public static string DescribeOutcome(this KeywordGlossary glossary, Outcome outcome, Optional<DamageType> superType) =>
        outcome.Match(
            grant => grant.Amount.Match(
                fraction => $"+{fraction.Amount.FormatPercent()} {grant.To.DescribeAbility()} energy",
                _ => $"refills {grant.To.DescribeAbility()}",
                byArchetype => $"+{byArchetype.Otherwise.FormatPercent()} {grant.To.DescribeAbility()} energy "
                    + $"({byArchetype.OnArchetypes.FormatPercent()} on {DescribeArchetypes(byArchetype.Archetypes)})",
                perStat => $"+{perStat.PerPoint.FormatPercent()} {grant.To.DescribeAbility()} energy per {perStat.Stat.DescribeStat()} point"),
            convert => $"spends {glossary.DescribeStatus(convert.Consumed)} → {DescribeStackEnergy(convert)}",
            apply => DescribeBuff(glossary, apply),
            remove => $"consumes {glossary.DescribeStatus(remove.Status)}",
            debuff => $"{glossary.DescribeStatus(debuff.Status)} target"
                + glossary.ReadShownDuration(debuff.Status, debuff.Duration).Match(d => $" ({d.Value.FormatSeconds()})", _ => ""),
            spawn => (spawn.Count > 1 ? $"{spawn.Count}× " : "") + glossary.DescribePickup(spawn.Pickup),
            summon => (summon.Count > 1 ? $"{summon.Count}× " : "") + glossary.DescribeSummon(summon.Summon),
            strike => $"{glossary.DescribeStatus(strike.Via)} strike" + (strike.Hit == HitOutcome.Kill ? " (kills)" : ""),
            modify => DescribeDamageChange(glossary, modify.Against, modify.Change, modify.Pvp, superType),
            heal => (heal.IncludesAllies ? "heals you and allies" : "heals you")
                + (heal.Pulses > 1 ? $" {heal.Pulses} ×" : "")
                + heal.Amount.Match(
                    share => share.Value is GameValue.Unknown ? $" {Unknown}" : $" {share.Value.FormatPercent()}",
                    points => $" {points.Value.FormatNumber()} HP"),
            reset => $"refills {reset.Which.DescribeAbility()}",
            collect => $"collects {glossary.DescribePickup(collect.Pickup)} from the ground",
            least => least.Amount.Match(
                fraction => $"+{fraction.Amount.FormatPercent()} energy to your least-charged ability",
                _ => "refills your least-charged ability",
                byArchetype => $"+{byArchetype.Otherwise.FormatPercent()} energy to your least-charged ability "
                    + $"({byArchetype.OnArchetypes.FormatPercent()} on {DescribeArchetypes(byArchetype.Archetypes)})",
                perStat => $"+{perStat.PerPoint.FormatPercent()} energy per {perStat.Stat.DescribeStat()} point to your least-charged ability"),
            spend => $"spends x{spend.Count.Value} {glossary.DescribeStatus(spend.Status)}",
            weapon => $"{glossary.DescribeDamageSource(weapon.Weapon, superType)} strike"
                + weapon.Type.Match(type => $", {type.Value} damage", _ => "")
                + (weapon.Hit == HitOutcome.Kill ? " (kills)" : ""),
            reload => DescribeWeaponAction("reloads", glossary, reload.Weapon, superType),
            refill => DescribeWeaponAction("refills", glossary, refill.Weapon, superType),
            ammo => $"+{ammo.Special.FormatPercent()} Special, +{ammo.Heavy.FormatPercent()} Heavy ammo progress");

    /// <summary>
    /// "reloads this weapon" (the catalog's text), "reloads your energy weapon" (bound to its slot), and "refills" the same
    /// way: no ammo is counted (ADRs D37, D42).
    /// </summary>
    private static string DescribeWeaponAction(string verb, KeywordGlossary glossary, DamageSource weapon, Optional<DamageType> superType) =>
        weapon is DamageSource.WeaponInSlot
            ? $"{verb} your {glossary.DescribeDamageSource(weapon, superType)}"
            : $"{verb} {glossary.DescribeDamageSource(weapon, superType)}";

    /// <summary>
    /// An outcome as it was applied: one that spent a counted buff's stacks says how many, and what they gave
    /// ("spends Armor Charge x2 · +1 (chance) → +28.4% grenade energy (34.4% with the chance stack)", ADRs D33); any other
    /// as <see cref="DescribeOutcome"/>.
    /// </summary>
    public static string DescribeAppliedOutcome(this KeywordGlossary glossary, Outcome outcome, Optional<StackSpend> spend, Optional<DamageType> superType) =>
        spend.Match(
            some => DescribeSpentOutcome(glossary, outcome, some.Value, superType),
            _ => glossary.DescribeOutcome(outcome, superType));

    private static string DescribeSpentOutcome(KeywordGlossary glossary, Outcome outcome, StackSpend spend, Optional<DamageType> superType)
    {
        var spent = $"{glossary.DescribeStatus(spend.Status)} {DescribeStacks(spend.Sure, spend.Chance)}";
        return outcome switch
        {
            Outcome.ConvertStacksToEnergy convert => $"spends {spent} → {DescribeSpentValue(spend, value => $"+{value.FormatPercent()} {convert.To.DescribeAbility()} energy")}",
            Outcome.RemoveBuff => $"consumes {spent}",
            Outcome.SpendStacks => $"spends {spent}",
            Outcome.ApplyBuff apply => DescribeBuff(glossary, apply) + DescribeChanceValue(spend, value => $"{value.FormatNumber()}s"),
            _ => glossary.DescribeOutcome(outcome, superType),
        };
    }

    /// <summary>
    /// An enemy count as the designer offers it: "1 enemy", "3 enemies"; what it is for when an equipped rule needs it from
    /// that ability or weapon — "2 in 3 s · Arc Siphon", "12 · Synergy", rules with different windows each saying theirs
    /// ("2 · Spark of Amplitude in 4 s, Harmonic Siphon in 3 s") — and what fills up at it: "10 enemies · Bolt Charge x10",
    /// "9 enemies · grenade full" (ADRs D4).
    /// </summary>
    public static string DescribeTargetCountChoice(this KeywordGlossary glossary, TargetCountChoice choice)
    {
        var count = choice.Count.Value;
        var windows = choice.NamedBy.Select(need => need.Within).Distinct().ToImmutableArray();
        var named = (choice.NamedBy.IsEmpty, windows.Length) switch
        {
            (true, _) => $"{count} {(count == 1 ? "enemy" : "enemies")}",
            (false, 1) => $"{count}{DescribeWindow(windows[0])} · {string.Join(", ", choice.NamedBy.Select(need => need.Name))}",
            _ => $"{count} · {string.Join(", ", choice.NamedBy.Select(need => need.Name + DescribeWindow(need.Within)))}",
        };
        return choice.Fills.IsEmpty ? named : $"{named} · {string.Join(", ", choice.Fills.Select(fill => glossary.DescribeCountFill(fill)))}";
    }

    /// <summary>"Bolt Charge x10", "grenade full": what an action's enemies fill up at a count.</summary>
    public static string DescribeCountFill(this KeywordGlossary glossary, CountFill fill) =>
        fill.Match(
            status => $"{glossary.DescribeStatus(status.Status)} x{status.Cap}",
            ability => $"{ability.Ability.DescribeAbility()} full");

    private static string DescribeWindow(Optional<Seconds> within) =>
        within.Match(some => $" in {some.Value.Value.ToString("0.#", Invariant)} s", _ => "");

    /// <summary>
    /// One change to a counted buff, with the step it came at and the stacks after it (ADRs D33): "#6 Orb of Power: +1 → x1",
    /// "#5 Orb of Power: +1 (chance) → x1 · +1 (chance)", "#7 you: at max → x4", "#8 Healthy Finisher: spends x1 → x2",
    /// "the pass before: x2" (what this pass started with).
    /// </summary>
    public static string DescribeStackChange(StackChange change)
    {
        var after = DescribeStacks(change.SureAfter, change.ChanceAfter);
        var source = change.Origin.Match(
            element => $"#{change.Step} {element.Name}",
            _ => $"#{change.Step} you",
            _ => "the pass before");
        return change.Move.Match(
            gained => gained.Sure.Value + gained.Chance.Value == 0
                ? $"{source}: at cap, +{gained.Wasted.Value} wasted → {after}"
                : $"{source}: {DescribeGain(gained)} → {after}" + (gained.Wasted.Value > 0 ? $" (at cap: +{gained.Wasted.Value} wasted)" : ""),
            spent => $"{source}: spends {DescribeStacks(spent.Sure, spent.Chance)} → {after}",
            _ => $"{source}: at max → {after}",
            _ => $"{source}: {after}");
    }

    private static string DescribeGain(StackMove.Gained gained) =>
        (gained.Sure.Value, gained.Chance.Value) switch
        {
            (var sure, 0) => $"+{sure}",
            (0, var chance) => $"+{chance} (chance)",
            (var sure, var chance) => $"+{sure} · +{chance} (chance)",
        };

    /// <summary>"x2", "x2 · +1 (chance)", "+1 (chance)": a counted buff's sure stacks and those it may hold besides (ADRs D33).</summary>
    public static string DescribeStacks(StackCount sure, StackCount chance) =>
        (sure.Value, chance.Value) switch
        {
            (0, 0) => "x0",
            (var s, 0) => $"x{s}",
            (0, var c) => $"+{c} (chance)",
            (var s, var c) => $"x{s} · +{c} (chance)",
        };

    /// <summary>The value for the sure stacks, and the value with the chance ones too ("(34.4% with the chance stack)"); "?" when the table has neither.</summary>
    private static string DescribeSpentValue(StackSpend spend, Func<GameValue, string> describe) =>
        spend.AtSure.Match(
            sure => describe(sure.Value) + DescribeChanceValue(spend, value => value.FormatPercent()),
            _ => spend.WithChance.Match(
                chance => describe(chance.Value),
                _ => describe(new GameValue.Unknown())));

    private static string DescribeChanceValue(StackSpend spend, Func<GameValue, string> describe) =>
        spend.AtSure.IsSome() && spend.Chance.Value > 0
            ? spend.WithChance.Match(
                chance => $" ({describe(chance.Value)} with the chance {(spend.Chance.Value == 1 ? "stack" : "stacks")})",
                _ => "")
            : "";

    /// <summary>"+?% grenade energy per stack", "+70% … 100% grenade energy at x3 … x6", "+16.2% … 45% grenade energy by copies and stacks (2 … 9)".</summary>
    private static string DescribeStackEnergy(Outcome.ConvertStacksToEnergy convert) =>
        convert.Energy.Match(
            perStack => $"+{perStack.Amount.FormatPercent()} {convert.To.DescribeAbility()} energy per stack",
            bySpent => $"+{DescribeTableRange(bySpent.Table, FormatTablePercent)} {convert.To.DescribeAbility()} energy at {DescribeTableCounts(bySpent.Table, "x")}",
            byCharges => $"+{DescribeTableRange(byCharges.Table, FormatTablePercent)} {convert.To.DescribeAbility()} energy by copies and stacks ({DescribeTableCounts(byCharges.Table, "")})");

    private static string FormatTablePercent(decimal value) =>
        new GameValue.Known(value).FormatPercent();

    private static string DescribeTableRange(StackTable table, Func<decimal, string> format) =>
        table.Values.Length == 1 ? format(table.Values[0]) : $"{format(table.Values[0])} … {format(table.Values[^1])}";

    private static string DescribeTableCounts(StackTable table, string prefix) =>
        table.Values.Length == 1
            ? $"{prefix}{table.From.Value}"
            : $"{prefix}{table.From.Value} … {prefix}{table.From.Value + table.Values.Length - 1}";

    /// <summary>
    /// Several outcomes of one rule as one phrase, with values narrowed to the copies equipped and
    /// same-amount energy grants merged: "+~15% grenade, melee and class ability energy".
    /// </summary>
    public static string DescribeOutcomes(this KeywordGlossary glossary, IEnumerable<OutcomeMention> mentions, Optional<DamageType> superType) =>
        string.Join(" and ", glossary.ListOutcomePhrases(mentions, superType).Select(phrase => phrase.Text));

    /// <summary>
    /// Each outcome of one rule as a phrase of its own (what <see cref="DescribeOutcomes"/> joins), values narrowed to
    /// the copies equipped and same-amount energy grants merged into one; each with the outcome it describes (a merged
    /// grant: its first).
    /// </summary>
    public static ImmutableArray<DescribedOutcome> ListOutcomePhrases(
        this KeywordGlossary glossary, IEnumerable<OutcomeMention> mentions, Optional<DamageType> superType)
    {
        var phrases = mentions
            .Select(mention => (Outcome: NarrowOutcomeToCopies(mention.Outcome, mention.Copies), mention.Spend))
            .Select(mention => new OutcomePhrase(
                glossary.DescribeAppliedOutcome(mention.Outcome, mention.Spend, superType),
                mention.Outcome,
                mention.Outcome is Outcome.GrantEnergy { Amount: EnergyGrant.Fraction fraction } grant
                    ? Optional.Some(new EnergyPhrase(fraction.Amount.FormatPercent(), grant.To))
                    : Optional.None<EnergyPhrase>()))
            .ToImmutableArray();
        var merged = phrases.Aggregate(ImmutableArray<OutcomePhrase>.Empty, MergeEnergyPhrase);
        return [.. merged.Select(phrase => new DescribedOutcome(phrase.Outcome, DescribeMergedPhrase(phrase)))];
    }

    private sealed record EnergyPhrase(string Amount, AbilityKind Ability);

    private sealed record OutcomePhrase(string Text, Outcome Outcome, Optional<EnergyPhrase> Energy, ImmutableArray<AbilityKind> MergedAbilities = default);

    private static ImmutableArray<OutcomePhrase> MergeEnergyPhrase(ImmutableArray<OutcomePhrase> merged, OutcomePhrase next)
    {
        var canMerge = !merged.IsEmpty
            && merged[^1].Energy.Bind(last => next.Energy.Map(n => n.Amount == last.Amount)).UnwrapOr(false);
        if (!canMerge)
        {
            return merged.Add(next with { MergedAbilities = next.Energy.Match(e => [e.Value.Ability], _ => ImmutableArray<AbilityKind>.Empty) });
        }

        var last = merged[^1];
        var abilities = last.MergedAbilities.AddRange(next.Energy.Match(e => [e.Value.Ability], _ => ImmutableArray<AbilityKind>.Empty));
        return merged.SetItem(merged.Length - 1, last with { MergedAbilities = abilities });
    }

    private static string DescribeMergedPhrase(OutcomePhrase phrase) =>
        phrase.Energy.Match(
            energy => phrase.MergedAbilities.Length > 1
                ? $"+{energy.Value.Amount} {string.Join(", ", phrase.MergedAbilities[..^1].Select(DescribeAbility))} and {phrase.MergedAbilities[^1].DescribeAbility()} energy"
                : phrase.Text,
            _ => phrase.Text);

    private static Outcome NarrowOutcomeToCopies(Outcome outcome, CopyCount copies) =>
        outcome switch
        {
            Outcome.GrantEnergy { Amount: EnergyGrant.Fraction fraction } grant =>
                grant with { Amount = new EnergyGrant.Fraction(fraction.Amount.NarrowToCopies(copies)) },
            Outcome.GrantLeastChargedEnergy { Amount: EnergyGrant.Fraction fraction } least =>
                least with { Amount = new EnergyGrant.Fraction(fraction.Amount.NarrowToCopies(copies)) },
            Outcome.ConvertStacksToEnergy { Energy: StackEnergy.PerStack perStack } convert =>
                convert with { Energy = new StackEnergy.PerStack(perStack.Amount.NarrowToCopies(copies)) },
            Outcome.ModifyDamage modify => modify with { Change = modify.Change.NarrowToCopies(copies), Pvp = modify.Pvp.Map(pvp => pvp.NarrowToCopies(copies)) },
            Outcome.RestoreHealth heal => heal with
            {
                Amount = heal.Amount.Match<HealAmount>(
                    share => new HealAmount.Share(share.Value.NarrowToCopies(copies)),
                    points => new HealAmount.Points(points.Value.NarrowToCopies(copies))),
            },
            Outcome.ApplyBuff apply => apply with { Stacks = apply.Stacks.NarrowToCopies(copies) },
            _ => outcome,
        };

    /// <summary>"+1 Bolt Charge" for a status that stacks (the grant, a fact), "Amplified (15s)" otherwise.</summary>
    private static string DescribeBuff(KeywordGlossary glossary, Outcome.ApplyBuff apply)
    {
        var name = glossary.DescribeStatus(apply.Status);
        var suffix = apply.Duration is BuffDuration.BySpent bySpent
            ? $" ({DescribeTableRange(bySpent.Table, value => $"{value.ToString("0.#", Invariant)}s")} at {DescribeTableCounts(bySpent.Table, "x")} spent)"
            : glossary.ReadShownDuration(apply.Status, apply.Duration.ReadFixedTime()).Match(d => $" ({d.Value.FormatSeconds()})", _ => "");
        return glossary.IsStacking(apply.Status) ? $"{DescribeStackGrant(apply.Stacks)} {name}{suffix}" : $"{name}{suffix}";
    }

    /// <summary>"+1", or by copies "+1 | +2" (an outcome not narrowed to the build's copies: the catalog's own text).</summary>
    private static string DescribeStackGrant(StackGrant grant) =>
        grant.Match(
            fixedCount => $"+{fixedCount.Count.Value}",
            perModCount => string.Join(" | ", perModCount.Counts.Select(count => $"+{count.Value}")));

    /// <summary>The duration an outcome shows: the rule's, else the glossary's — a fact, never counted down.</summary>
    private static Optional<Seconds> ReadShownDuration(this KeywordGlossary glossary, StatusId status, Optional<Seconds> stated) =>
        stated.IsSome()
            ? stated
            : glossary.Statuses.TryGetValue(status, out var definition) ? definition.Duration : Optional.None<Seconds>();

    /// <summary>
    /// A status's facts: "up to x10" (the build's cap: Charged Up raises Armor Charge's), "15s" from the glossary — shown,
    /// never counted (ADRs D1); none when the glossary gives neither.
    /// </summary>
    public static Optional<string> DescribeStatusFacts(this ValidatedBuild build, StatusId status) =>
        build.DescribeStatusFacts(status, Optional.None<Seconds>());

    /// <summary>A status's facts with the duration its giver states (the enhanced Stats for All's 11s), else the glossary's.</summary>
    public static Optional<string> DescribeStatusFacts(this ValidatedBuild build, StatusId status, Optional<Seconds> stated)
    {
        var duration = build.Catalog.Glossary.ReadShownDuration(status, stated);
        var facts = new[] { build.DescribeStackCap(status).Map(cap => $"up to {cap}"), duration.Map(FormatSeconds) }
            .OfType<Optional<string>.Some>()
            .Select(fact => fact.Value)
            .ToImmutableArray();
        return facts.IsEmpty ? Optional.None<string>() : Optional.Some(string.Join(" · ", facts));
    }

    /// <summary>"x4": the build's cap of a status that stacks (<see cref="GlossaryReading.ReadStackCap"/>), a fact; none for one that doesn't.</summary>
    public static Optional<string> DescribeStackCap(this ValidatedBuild build, StatusId status) =>
        build.ReadStackCap(status).Map(cap => $"x{cap.FormatNumber()}");

    /// <summary>"Arc buff", "Strand weapon buff", "Arc debuff": what a status is, as a label (a neutral one: "Buff").</summary>
    public static string DescribeStatusKind(this KeywordGlossary glossary, StatusId status) =>
        glossary.Statuses.TryGetValue(status, out var definition)
            ? ToSentenceStart(DescribeAffinityPrefix(definition.Affinity) + definition.Kind switch
            {
                KeywordKind.WeaponBuff => "weapon buff",
                KeywordKind.Debuff => "debuff",
                _ => "buff",
            })
            : "Status";

    /// <summary>
    /// What a keyword is in the game, as a label: a status's kind ("Arc buff", "Strand weapon buff", "Arc debuff"), an
    /// elemental pickup ("Arc elemental pickup": Ionic Traces, Firesprites, Tangles…), a plain pickup (an Orb of Power:
    /// "Pickup"), a summon ("Strand summon").
    /// </summary>
    public static string DescribeKeywordKind(this KeywordGlossary glossary, KeywordRef keyword) =>
        keyword.Match(
            status => glossary.DescribeStatusKind(status.Id),
            pickup => glossary.Pickups.TryGetValue(pickup.Id, out var definition) && definition.Affinity != Affinity.Neutral
                ? $"{definition.Affinity} elemental pickup"
                : "Pickup",
            summon => glossary.Summons.TryGetValue(summon.Id, out var definition)
                ? $"{definition.DamageType} summon"
                : "Summon");

    private static string DescribeAffinityPrefix(Affinity affinity) =>
        affinity == Affinity.Neutral ? "" : $"{affinity} ";

    private static string ToSentenceStart(string text) =>
        char.ToUpperInvariant(text[0]) + text[1..];

    /// <summary>
    /// A buff on you as the state shows it: "Bolt Charge", or "Bolt Charge (at max)" once the player declared it; a
    /// counted buff with its stacks, "Armor Charge x1 · +1 (chance)" (ADRs D33).
    /// </summary>
    public static string DescribeActiveBuff(this KeywordGlossary glossary, ActiveBuff buff) =>
        glossary.DescribeStatus(buff.Status)
        + buff.Tally.Match(tally => $" {DescribeStacks(tally.Value.Sure, tally.Value.Chance)}", _ => "")
        + (buff.AtMax ? " (at max)" : "");

    /// <summary>
    /// A passive as a fact, its values narrowed to the <paramref name="copies"/> equipped (Weapon Surge with two copies:
    /// "+17% Arc weapon damage"), as a rule's outcomes are; <paramref name="superType"/>: see <see cref="DescribeDamageSource"/>.
    /// </summary>
    public static string DescribePassive(this KeywordGlossary glossary, Passive passive, CopyCount copies, Optional<DamageType> superType) =>
        NarrowPassiveToCopies(passive, copies).Match(
            extra => $"+{extra.Extra.Value} {glossary.DescribeStatus(extra.Status)} per gain",
            charges => $"+{charges.Extra} {charges.Ability.DescribeAbility()} charge",
            modify => DescribeDamageChange(glossary, modify.Against, modify.Change, modify.Pvp, superType),
            resist => DescribeResistance(resist),
            weapons => $"{DescribeChangedWeapons(glossary, weapons.Weapons, superType)}: "
                + string.Join(", ", weapons.Changes.Select(change => DescribeStatChange(change))),
            raise => $"{glossary.DescribeStatus(raise.Status)} up to x{raise.Max.FormatNumber()}",
            stat => $"{FormatSignedNumber(stat.Change)} {stat.Stat}",
            combatant => $"{combatant.Amount.FormatPercent()} damage resistance against {combatant.Against.DescribeCombatantDamage(superType)}",
            regeneration => DescribeRegeneration(regeneration));

    /// <summary>"400% [PvP 200%] faster grenade and melee regeneration": with its PvP value when the source gives one.</summary>
    private static string DescribeRegeneration(Passive.FasterRegeneration regeneration) =>
        regeneration.Rate.Pve.FormatPercent()
        + regeneration.Rate.Pvp.Map(pvp => $" [PvP {pvp.FormatPercent()}]").UnwrapOr("")
        + $" faster {JoinWithAnd(regeneration.Abilities.Select(ability => ability.DescribeAbility()).ToImmutableArray())} regeneration";

    /// <summary>"45% [PvP 25%] damage resistance": against any damage, with its PvP value when the source gives one.</summary>
    private static string DescribeResistance(Passive.ResistDamage resist) =>
        resist.Amount.Pve.FormatPercent()
        + resist.Amount.Pvp.Map(pvp => $" [PvP {pvp.FormatPercent()}]").UnwrapOr("")
        + " damage resistance";

    /// <summary>
    /// The combatants' damage a resistance reduces: "combatants", "Arc combatants", "combatants within 5 m", "combatants
    /// beyond 30 m", "combatants' area, blast and explosive damage"; the Super's element by name when known.
    /// </summary>
    public static string DescribeCombatantDamage(this CombatantDamage against, Optional<DamageType> superType) =>
        against.Match(
            _ => "combatants",
            type => $"{type.Type} combatants",
            _ => superType
                .Bind(type => type == DamageType.Kinetic ? Optional.None<string>() : Optional.Some($"{type} combatants"))
                .UnwrapOr("combatants of your Super's element"),
            within => $"combatants within {within.Meters.ToString("0.#", Invariant)} m",
            beyond => $"combatants beyond {beyond.Meters.ToString("0.#", Invariant)} m",
            _ => "combatants' area, blast and explosive damage");

    /// <summary>The same passive with each of its per-copy values narrowed to the copies equipped (<see cref="GameValueArithmetic.NarrowToCopies"/>).</summary>
    private static Passive NarrowPassiveToCopies(Passive passive, CopyCount copies) =>
        passive switch
        {
            Passive.ModifyDamage modify => modify with { Change = modify.Change.NarrowToCopies(copies), Pvp = modify.Pvp.Map(pvp => pvp.NarrowToCopies(copies)) },
            Passive.ResistDamage resist => resist with
            {
                Amount = new PveAndPvp(resist.Amount.Pve.NarrowToCopies(copies), resist.Amount.Pvp.Map(pvp => pvp.NarrowToCopies(copies))),
            },
            Passive.ModifyWeaponStats weapons => weapons with
            {
                Changes = [.. weapons.Changes.Select(change => change with { Change = change.Change.NarrowToCopies(copies) })],
            },
            Passive.RaiseMaxStacks raise => raise with { Max = raise.Max.NarrowToCopies(copies) },
            Passive.ModifyStat stat => stat with { Change = stat.Change.NarrowToCopies(copies) },
            Passive.ResistCombatantDamage combatant => combatant with { Amount = combatant.Amount.NarrowToCopies(copies) },
            Passive.FasterRegeneration regeneration => regeneration with
            {
                Rate = new PveAndPvp(regeneration.Rate.Pve.NarrowToCopies(copies), regeneration.Rate.Pvp.Map(pvp => pvp.NarrowToCopies(copies))),
            },
            _ => passive,
        };

    /// <summary>"+20", "+20|40|50", "+~20", "+?" — and "-10" for a value that lowers: a stat change as the game shows it.</summary>
    private static string FormatSignedNumber(GameValue value) =>
        value.Match(
            known => known.Value < 0,
            perModCount => perModCount.Values.Any(v => v < 0),
            approximate => approximate.Value < 0,
            _ => false)
            ? value.FormatNumber()
            : "+" + value.FormatNumber();

    /// <summary>"Linear Fusion Rifles, Fusion Rifles and Heat Weapons", "this weapon", "Kinetic weapon": what a change to weapon stats reaches.</summary>
    public static string DescribeChangedWeapons(this KeywordGlossary glossary, ChangedWeapons weapons, Optional<DamageType> superType) =>
        weapons.Match(
            ofArchetypes => DescribeArchetypes(ofArchetypes.Archetypes),
            _ => Capitalize(glossary.DescribeDamageSource(new DamageSource.ThisWeapon(), superType)),
            inSlot => Capitalize(glossary.DescribeDamageSource(new DamageSource.WeaponInSlot(inSlot.Slot), superType)));

    /// <summary>"Linear Fusion Rifles, Fusion Rifles and Heat Weapons": weapon archetypes as the game names them, plural.</summary>
    public static string DescribeArchetypes(ImmutableArray<string> archetypes) =>
        JoinWithAnd(archetypes.Select(archetype => DescribeArchetype(archetype) + "s").ToImmutableArray());

    /// <summary>"+35 Handling", "+? Handling" (the source gives no number: "?", never 0): stat points, as the game shows them.</summary>
    private static string DescribeStatChange(WeaponStatChange change) =>
        $"{FormatSignedNumber(change.Change)} {ToTitleWords(change.Stat)}";

    /// <summary>"a", "a and b", "a, b and c".</summary>
    private static string JoinWithAnd(ImmutableArray<string> parts) =>
        parts.Length <= 1 ? string.Concat(parts) : $"{string.Join(", ", parts[..^1])} and {parts[^1]}";

    // ── events and actions ─────────────────────────────────────────────────────

    /// <summary>
    /// An event with the rules it set off: the event, and for kills or hits of several enemies in one action, the tightest
    /// window a rule it fired needs ("Festival Flight killed 2 enemies in 3 s": Strand Siphon's two kills within 3 s; 2 kills
    /// within 3 s are within 4 s too; "hit 3 enemies in 3 s": One For All).
    /// </summary>
    public static string DescribeFiredEvent(this KeywordGlossary glossary, ImmutableArray<FiredRule> rules, Build build) =>
        glossary.DescribeEvent(rules[0].Trigger, build)
            + DescribeWindow(rules
                .Select(rule => rule.On switch
                {
                    Trigger.KillMultiple kill => kill.Within,
                    Trigger.DamageMultiple hit => hit.Within,
                    _ => Optional.None<Seconds>(),
                })
                .Where(within => within.IsSome())
                .OrderBy(within => within.Match(some => some.Value.Value, _ => 0m))
                .FirstOrDefault(Optional.None<Seconds>()));

    public static string DescribeEvent(this KeywordGlossary glossary, GameEvent gameEvent, Build build) =>
        gameEvent.Match(
            cast => DescribeCast(cast.Kind, cast.Input),
            damaged => $"{glossary.DescribeOrigin(damaged.Origin, build)} 🥊 hit{DescribeOnTarget(DescribeTargetPhrase(glossary, damaged.TargetHas))}",
            killed => $"{glossary.DescribeOrigin(killed.Origin, build)} 💀 kill{DescribeOnTarget(DescribeTargetPhrase(glossary, killed.TargetHas))}",
            struck => $"{glossary.DescribeOrigin(struck.Origin, build)} {(struck.Hit == HitOutcome.Kill ? "💀 killed" : "🥊 hit")} {DescribeEnemies(struck.Targets)}",
            pickedUp => $"Picked up {glossary.DescribePickup(pickedUp.Pickup)}",
            gained => $"{glossary.DescribeStatus(gained.Status)} gained",
            maxed => $"Max {glossary.DescribeStatus(maxed.Status)}",
            _ => Finisher,
            _ => CriticalHealth,
            broken => $"{glossary.DescribeOrigin(broken.Origin, build)} 🛡 broke a shield",
            expended => expended.Kind.DescribeEnergyExpended(),
            reloaded => $"{glossary.DescribeOrigin(reloaded.Origin, build)} reloaded",
            _ => HitAtCriticalHealth,
            ammo => $"{(ammo.Pickup == BrickPickup.Slide ? "Slid over" : "Picked up")} a {ammo.Brick} ammo brick",
            readied => $"Readied {glossary.DescribeOrigin(readied.Origin, build)}");

    /// <summary>
    /// "Grenade (💀 kill)", "Grenade (💀 kill 3)", "Grenade (💀 kill · ⬜ spend fully)", "Festival Flight (🥊 hit 5)",
    /// "Festival Flight (💀 kill · 🛡 shield)", "Festival Flight (💀 kill · 🎯 precision)", "Class ability", "Class ability (⬜ spend fully)", "Pick up Orb of Power",
    /// "Bolt Charge at max (x10)", "Amplified ends", "New pack", "Critical health", "Hit at critical health", "Finisher", "Reload Festival Flight",
    /// "Pick up a Heavy ammo brick", "Slide over a Special ammo brick", "Ready Buzzard".
    /// </summary>
    public static string DescribeAction(this ValidatedBuild build, PlayerAction action) =>
        action.Match(
            cast => $"{Capitalize(cast.Kind.ToAbilityKind().DescribeAbilityUse(cast.Input))} ({DescribeHit(cast.Hit, cast.Targets)}{DescribeChargeOption(cast.Charge)}{DescribeRankOption(cast.Rank)})",
            use => (use.Input, use.Charge) switch
            {
                (AbilityInput.Air, ChargeUse.Last) => $"Class ability (in the air · {LastChargeOption})",
                (AbilityInput.Air, _) => "Class ability (in the air)",
                (_, ChargeUse.Last) => $"Class ability ({LastChargeOption})",
                _ => "Class ability",
            },
            fire => $"{DescribeWeapon(build.Build, fire.Slot, fire.Slot.ToString())} ({DescribeHit(fire.Hit, fire.Targets)}{(fire.Shield == ShieldBreak.Breaks ? $" · {ShieldOption}" : "")}{(fire.Zone == HitZone.Precision ? $" · {PrecisionOption}" : "")}{DescribeRankOption(fire.Rank)})",
            collect => $"Pick up {build.Catalog.Glossary.DescribePickup(collect.Pickup)}",
            declare => build.DescribeDeclaration(declare.Declaration),
            finisher => finisher.Rank == EnemyTier.Minor ? Finisher : $"{Finisher} ({finisher.Rank.DescribeRank()})",
            reload => $"{Reload} {DescribeWeapon(build.Build, reload.Slot, reload.Slot.ToString())}",
            ammo => DescribeAmmoPickup(Optional.Some(ammo.Brick), ammo.Pickup),
            ready => $"{ReadyWeapon} {DescribeWeapon(build.Build, ready.Slot, ready.Slot.ToString())}");

    /// <summary>"Armor Charge at max (x4)": the max names the build's cap (Charged Up raises it), a fact the player declares reached.</summary>
    private static string DescribeDeclaration(this ValidatedBuild build, StateDeclaration declaration) =>
        declaration.Match(
            max => $"{build.Catalog.Glossary.DescribeStatus(max.Status)} at max" + build.DescribeStackCap(max.Status).Match(cap => $" ({cap.Value})", _ => ""),
            end => $"{build.Catalog.Glossary.DescribeStatus(end.Status)} ends",
            _ => "New pack",
            _ => CriticalHealth,
            _ => HitAtCriticalHealth,
            recharged => $"{Capitalize(recharged.Ability.DescribeAbility())} recharged");

    /// <summary>
    /// The status a declaration is about, beside whose name the designer shows its button: a <c>max:</c>'s or an
    /// <c>end:</c>'s own, critical health for a hit at it; none for a new pack or reaching critical health.
    /// </summary>
    public static Optional<StatusId> FindDeclaredStatus(this StateDeclaration declaration) =>
        declaration.Match(
            max => Optional.Some(max.Status),
            end => Optional.Some(end.Status),
            _ => Optional.None<StatusId>(),
            _ => Optional.None<StatusId>(),
            _ => Optional.Some(DeclaredStatuses.CriticalHealth),
            _ => Optional.None<StatusId>());

    /// <summary>
    /// A declaration's caption beside its status's name: its label without the name ("Bolt Charge at max (x10)" → "at max
    /// (x10)", "Amplified ends" → "ends"), "hit" for a hit at critical health.
    /// </summary>
    public static string DescribeStatusCaption(this StateDeclaration declaration, string label, string statusName) =>
        declaration is StateDeclaration.HitAtCriticalHealth
            ? HitAtCriticalHealthCaption
            : label.StartsWith(statusName + " ", StringComparison.Ordinal) ? label[(statusName.Length + 1)..] : label;

    /// <summary>
    /// "💀 kill", "🥊 hit", and with more than one target "💀 kill 3", "🥊 hit 5": the mark tells a kill step from a hit
    /// step at a glance (a hit step is damage kept up, a kill step's hit the killing blow, ADRs D34).
    /// </summary>
    private static string DescribeHit(HitOutcome hit, TargetCount targets) =>
        (hit == HitOutcome.Kill ? "💀 kill" : "🥊 hit") + (targets.Value > 1 ? $" {targets.Value}" : "");

    /// <summary>" · ⬜ spend fully" for the use that spends an ability's last charge (ADRs D36); nothing for a plain use.</summary>
    private static string DescribeChargeOption(ChargeUse charge) =>
        charge == ChargeUse.Last ? $" · {LastChargeOption}" : "";

    /// <summary>"1 enemy", "3 enemies".</summary>
    private static string DescribeEnemies(TargetCount targets) =>
        targets.Value == 1 ? "1 enemy" : $"{targets.Value} enemies";

    /// <summary>
    /// The action's token, the same everywhere (loop files, share links, the web): <c>grenade:kill</c>, <c>grenade:kill:3</c>,
    /// <c>class</c>, <c>kinetic</c>, <c>kinetic:hit:5</c>, <c>pickup:orb-of-power</c>, <c>max:bolt-charge</c>,
    /// <c>end:amplified</c>, <c>pack:new</c>, <c>health:critical</c>, <c>health:hit</c>, <c>finisher</c>, <c>kinetic:reload</c>,
    /// <c>ammo:heavy</c>, <c>ammo:special:slide</c>, <c>kinetic:ready</c>. The target count is written only when it is more than one; every
    /// token round-trips. An option goes last: <c>:last</c> on an ability's last charge (<c>grenade:kill:last</c>),
    /// <c>:shield</c> on a weapon step that breaks a shield (<c>kinetic:kill:3:shield</c>), then <c>:precision</c> on one whose
    /// hits and kills are precision ones (<c>energy:kill:precision</c>, <c>energy:kill:shield:precision</c>), and last the rank
    /// of a stronger enemy a kill step or a finisher kills (<c>energy:kill:elite</c>, <c>grenade:kill:last:boss</c>,
    /// <c>finisher:elite</c>) (ADRs D45).
    /// </summary>
    public static string ToActionToken(this PlayerAction action) =>
        action.Match(
            cast => cast.Kind.ToString().ToLowerInvariant() + ToInputSuffix(cast.Input) + ToHitSuffix(cast.Hit, cast.Targets) + ToChargeSuffix(cast.Charge) + ToRankSuffix(cast.Rank),
            use => "class" + ToInputSuffix(use.Input) + ToChargeSuffix(use.Charge),
            fire => fire.Slot.ToString().ToLowerInvariant() + ToHitSuffix(fire.Hit, fire.Targets) + (fire.Shield == ShieldBreak.Breaks ? ":shield" : "") + (fire.Zone == HitZone.Precision ? ":precision" : "") + ToRankSuffix(fire.Rank),
            collect => $"pickup:{collect.Pickup}",
            declare => declare.Declaration.Match(
                max => $"max:{max.Status}",
                end => $"end:{end.Status}",
                _ => "pack:new",
                _ => "health:critical",
                _ => "health:hit",
                recharged => $"recharged:{ToAbilityWord(recharged.Ability)}"),
            finisher => "finisher" + ToRankSuffix(finisher.Rank),
            reload => reload.Slot.ToString().ToLowerInvariant() + ":reload",
            ammo => $"ammo:{ammo.Brick.ToString().ToLowerInvariant()}" + (ammo.Pickup == BrickPickup.Slide ? ":slide" : ""),
            ready => ready.Slot.ToString().ToLowerInvariant() + ":ready");

    /// <summary>The ability's word in a token: <c>grenade</c>, <c>melee</c>, <c>class</c>, <c>super</c>.</summary>
    public static string ToAbilityWord(AbilityKind ability) =>
        ability == AbilityKind.ClassAbility ? "class" : ability.ToString().ToLowerInvariant();

    /// <summary>":elite", ":miniboss", ":champion", ":boss" for a kill of a stronger enemy than the pack's minors; nothing for a minor.</summary>
    private static string ToRankSuffix(EnemyTier rank) =>
        rank == EnemyTier.Minor ? "" : ":" + rank.ToString().ToLowerInvariant();

    /// <summary>":last" for the use that spends an ability's last charge; nothing for a plain use.</summary>
    private static string ToChargeSuffix(ChargeUse charge) =>
        charge == ChargeUse.Last ? ":last" : "";

    /// <summary>":slide", ":air", ":hold" for a secondary; nothing for a press.</summary>
    private static string ToInputSuffix(AbilityInput input) =>
        input == AbilityInput.Press ? "" : ":" + input.ToInputWord();

    private static string ToHitSuffix(HitOutcome hit, TargetCount targets) =>
        (hit, targets.Value) switch
        {
            (HitOutcome.Kill, 1) => ":kill",
            (HitOutcome.Kill, var count) => $":kill:{count.ToString(Invariant)}",
            (_, 1) => "",
            (_, var count) => $":hit:{count.ToString(Invariant)}",
        };

    // ── loop analysis ──────────────────────────────────────────────────────────

    /// <summary>"Attrition Orbs", or "Attrition Orbs (chance)" when it fired only by chance there (ADRs D8).</summary>
    public static string DescribeMention(this ElementMention mention) =>
        mention.Name + (mention.Likelihood == Likelihood.Chance ? " (chance)" : "");

    /// <summary>"Tempest Strike — doesn't stack with Dielectric": a rule that gave way there (ADRs D6).</summary>
    public static string DescribeWasted(this WastedMention wasted) =>
        $"{wasted.Source.DescribeMention()} — doesn't stack with {wasted.PartnerName}";

    /// <summary>"← #2", "← previous pass #2" (step numbers from 1, as the player reads them).</summary>
    public static string DescribeProvider(this NeedProvider provider) =>
        provider.Match(
            earlier => $"← #{earlier.StepIndex + 1}",
            previous => $"← previous pass #{previous.StepIndex + 1}");

    /// <summary>"Reaper ← #1 [Reaper]", "Orb of Power ← #2 [Reaper; Strand Siphon (chance)]", "Bolt Charge at max ← #6".</summary>
    public static string DescribeNeed(this StepNeed need)
    {
        var elements = need.Provider.Match(earlier => earlier.Elements, previous => previous.Elements);
        var sources = elements.IsEmpty ? "" : $" [{string.Join("; ", elements.Select(DescribeMention))}]";
        return $"{need.What} {need.Provider.DescribeProvider()}{sources}";
    }

    /// <summary>"#2", "previous pass #6": where a link of the step chain starts.</summary>
    public static string DescribeLinkSource(this StepLink link) =>
        link.FromPreviousPass ? $"previous pass #{link.From + 1}" : $"#{link.From + 1}";

    /// <summary>"#2 → #4: Slice · Reaper", "previous pass #6 → #3: Armor Charge": a link of the step chain.</summary>
    public static string DescribeLink(this StepLink link) =>
        $"{link.DescribeLinkSource()} → #{link.To + 1}: {link.DescribeLinkNeeds()}";

    /// <summary>"Slice · Reaper ← #2": a link as the step that needs it reads it.</summary>
    public static string DescribeIncomingLink(this StepLink link) =>
        $"{link.DescribeLinkNeeds()} ← {link.DescribeLinkSource()}";

    /// <summary>"#5: Sever · Unravel", "next pass #3: Armor Charge": a link as the step that provided it reads it.</summary>
    public static string DescribeOutgoingLink(this StepLink link) =>
        $"{link.DescribeLinkTarget()}: {link.DescribeLinkNeeds()}";

    /// <summary>A step with no incoming link.</summary>
    public const string NoNeedsFromEarlierSteps = "nothing from earlier steps";

    /// <summary>A step with no outgoing link.</summary>
    public const string FeedsNoLaterStep = "no later step";

    /// <summary>"#4", "next pass #3": where a link of the step chain ends, seen from the step that provided it.</summary>
    public static string DescribeLinkTarget(this StepLink link) =>
        link.FromPreviousPass ? $"next pass #{link.To + 1}" : $"#{link.To + 1}";

    /// <summary>"Slice · Reaper", "Armor Charge (chance)": what flows along a link, a need that came only from chance rules marked.</summary>
    public static string DescribeLinkNeeds(this StepLink link) =>
        string.Join(" · ", link.Needs.Select(need => need.What + (IsChanceOnly(need) ? " (chance)" : "")));

    private static bool IsChanceOnly(StepNeed need)
    {
        var elements = need.Provider.Match(earlier => earlier.Elements, previous => previous.Elements);
        return !elements.IsEmpty && elements.All(element => element.Likelihood == Likelihood.Chance);
    }

    /// <summary>The verdict on a loop's order (docs/loop-format.md, "Analysis").</summary>
    public static string DescribeVerdict(this LoopVerdict verdict) =>
        verdict.Match(
            _ => "The loop has no steps.",
            repeats => repeats.FirstPassBlocked.Match(
                blocked => $"✓ Repeats — on the first pass, #{blocked.Value.StepIndex + 1} ({blocked.Value.Label}) can't happen yet: {blocked.Value.Reason}",
                _ => "✓ Repeats — each pass ends with what the next one needs"),
            breaks => $"✗ Breaks at #{breaks.Blocked.StepIndex + 1} ({breaks.Blocked.Label}): {breaks.Blocked.Reason}");

    /// <summary>"✓ Repeats", "✗ Breaks at #3", "No steps" — the verdict in a word or two (the analysis badge).</summary>
    public static string DescribeShortVerdict(this LoopVerdict verdict) =>
        verdict.Match(
            _ => "No steps",
            _ => "✓ Repeats",
            breaks => $"✗ Breaks at #{breaks.Blocked.StepIndex + 1}");

    // ── the artifact (Artifacts 2.0) ───────────────────────────────────────────

    /// <summary>"Tier 2": an artifact tier's column heading.</summary>
    public static string DescribeArtifactTier(int tier) =>
        $"Tier {tier.ToString(Invariant)}";

    /// <summary>"Tier 1", "Tier 1–2": the tiers a perk slot takes (up to <paramref name="tier"/>).</summary>
    public static string DescribeArtifactSlot(int tier) =>
        tier == 1 ? DescribeArtifactTier(1) : $"Tier 1–{tier.ToString(Invariant)}";

    /// <summary>"◆◆": the game's mark over a group of perk slots, one diamond for each tier the slots take.</summary>
    public static string DescribeArtifactSlotMark(int tier) =>
        new('◆', Math.Max(tier, 1));

    public static string Capitalize(string text) =>
        text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
