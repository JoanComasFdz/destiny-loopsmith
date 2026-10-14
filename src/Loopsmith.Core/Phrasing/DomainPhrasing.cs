using System.Collections.Immutable;
using System.Globalization;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

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

    public static string FormatNumber(this GameValue value) =>
        value.Match(
            known => known.Value.ToString("0.##", Invariant),
            perModCount => string.Join("|", perModCount.Values.Select(v => v.ToString("0.##", Invariant))),
            approximate => "~" + approximate.Value.ToString("0.##", Invariant),
            _ => Unknown);

    public static string FormatSeconds(this Seconds seconds) =>
        seconds.Value.ToString("0.#", Invariant) + "s";

    private static string FormatPercentNumber(decimal fraction) =>
        (fraction * 100m).ToString("0.#", Invariant) + "%";

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
    /// What a mod socket takes, in short (an empty socket's square): its slot ("Helmet"), "General", "Artifice", "Raid",
    /// else "Special" (an activity's).
    /// </summary>
    public static string DescribeModSocketShort(this PlugCategoryId category) =>
        ArmorModCategories.FindSlot(category).Match(
            slot => slot.Value.DescribeArmorSlot(),
            _ => category == ArmorModCategories.General ? "General"
                : category == ArmorModCategories.Artifice ? "Artifice"
                : category.Value.StartsWith(ArmorModCategories.RaidPrefix, StringComparison.Ordinal) ? "Raid"
                : "Special");

    /// <summary>What a mod socket takes, as the game words its mods ("Helmet mod", "General mod", "Artifice mod"); none for a raid's or an activity's (its mods' type says).</summary>
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

    /// <summary>"3 items, 1 subclass plug, 2 armor mods": what a DIM loadout had that its build leaves out.</summary>
    public static string DescribeLeftOut(this ImmutableArray<LeftOutItem> leftOut) =>
        string.Join(", ", leftOut
            .GroupBy(item => item.Part)
            .OrderBy(group => group.Key)
            .Select(group => $"{group.Count()} {group.Key.DescribeLoadoutPart()}{(group.Count() == 1 ? "" : "s")}"));

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

    /// <summary>"Jolt" → "Jolted", "Sever" → "Severed" — the adjective a player uses for a debuffed target.</summary>
    public static string DescribeDebuffedAdjective(this KeywordGlossary glossary, StatusId status)
    {
        var name = glossary.DescribeStatus(status);
        return name switch
        {
            _ when name.Contains(' ') => name,
            _ when name.EndsWith('e') => name + "d",
            _ when name.EndsWith("ed", StringComparison.Ordinal) => name,
            _ => name + "ed",
        };
    }

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
            thisOfType => $"this weapon ({thisOfType.Type})");

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

    // ── triggers, conditions, outcomes, passives ───────────────────────────────

    /// <summary>A rule's trigger as a player says it; <paramref name="superType"/>: see <see cref="DescribeDamageSource"/>.</summary>
    public static string DescribeTrigger(this KeywordGlossary glossary, Trigger trigger, Optional<DamageType> superType) =>
        trigger.Match(
            cast => DescribeCast(cast.Kind),
            killAny => DescribeKill(glossary, killAny.Via, [], superType),
            killOfTier => $"{DescribeKill(glossary, killOfTier.Via, [], superType)} ({killOfTier.Tier})",
            killDebuffed => DescribeKill(glossary, killDebuffed.Via, killDebuffed.TargetHas, superType),
            killMultiple => DescribeMultiKill(glossary, killMultiple.Via, killMultiple.AtLeast, superType),
            damage => DescribeDamage(glossary, damage.Via, [], superType),
            damageDebuffed => DescribeDamage(glossary, damageDebuffed.Via, damageDebuffed.TargetHas, superType),
            damageMultiple => DescribeMultiHit(glossary, damageMultiple.Via, damageMultiple.AtLeast, superType),
            pickUp => $"Pick up {glossary.DescribePickup(pickUp.Pickup)}",
            buffGained => $"Gain {glossary.DescribeStatus(buffGained.Status)}",
            stacksMaxed => $"Max {glossary.DescribeStatus(stacksMaxed.Status)}",
            _ => Finisher,
            castAs => DescribeCast(castAs.Kind, castAs.Input),
            killFewer => DescribeFewKills(glossary, killFewer.Via, killFewer.FewerThan, superType),
            _ => CriticalHealth);

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
    private static string DescribeMultiKill(KeywordGlossary glossary, DamageSource via, TargetCount atLeast, Optional<DamageType> superType) =>
        via is DamageSource.AnySource
            ? $"Kill {atLeast.Value}+ enemies"
            : $"Kill {atLeast.Value}+ with {glossary.DescribeDamageSource(via, superType)}";

    /// <summary>"Kill 1–11 with this weapon", "Kill 1–11 enemies" (any source): fewer than N in one action, at least one.</summary>
    private static string DescribeFewKills(KeywordGlossary glossary, DamageSource via, TargetCount fewerThan, Optional<DamageType> superType)
    {
        var most = fewerThan.Value - 1;
        var range = most == 1 ? "1" : $"1–{most.ToString(Invariant)}";
        return via is DamageSource.AnySource
            ? $"Kill {range} {(most == 1 ? "enemy" : "enemies")}"
            : $"Kill {range} with {glossary.DescribeDamageSource(via, superType)}";
    }

    /// <summary>"Hit 3+ enemies with weapon", "Hit 3+ enemies" (any source).</summary>
    private static string DescribeMultiHit(KeywordGlossary glossary, DamageSource via, TargetCount atLeast, Optional<DamageType> superType) =>
        via is DamageSource.AnySource
            ? $"Hit {atLeast.Value}+ enemies"
            : $"Hit {atLeast.Value}+ enemies with {glossary.DescribeDamageSource(via, superType)}";

    /// <summary>"+20% Arc weapon damage"; a weapon of your Super's element reads "+20% damage with Arc weapon (your Super's element)".</summary>
    private static string DescribeDamageChange(KeywordGlossary glossary, DamageSource against, GameValue change, Optional<DamageType> superType) =>
        against is DamageSource.WeaponOfSuperType
            ? $"+{change.FormatPercent()} damage with {glossary.DescribeDamageSource(against, superType)}"
            : $"+{change.FormatPercent()} {glossary.DescribeDamageSource(against, superType)} damage";

    private static string DescribeTargetPhrase(KeywordGlossary glossary, ImmutableArray<StatusId> targetHas) =>
        targetHas.IsEmpty
            ? "target"
            : string.Join(" + ", targetHas.Select(glossary.DescribeDebuffedAdjective)) + " target";

    /// <summary>The ability-cast trigger as a player says it ("Grenade thrown", "Melee swung while sliding", "Grenade held").</summary>
    private static string DescribeCast(AbilityKind kind, AbilityInput input) =>
        (kind, input) switch
        {
            (_, AbilityInput.Press) => DescribeCast(kind),
            (AbilityKind.ClassAbility, AbilityInput.Air) => "Class ability in the air",
            (AbilityKind.Melee, AbilityInput.Slide) => "Melee swung while sliding",
            (AbilityKind.Melee, AbilityInput.Air) => "Melee swung in the air",
            (AbilityKind.Grenade, AbilityInput.Hold) => "Grenade held",
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
            belowMax => $"while {glossary.DescribeStatus(belowMax.Status)} below max");

    public static string DescribeConditions(this KeywordGlossary glossary, ImmutableArray<Condition> conditions) =>
        string.Join(", ", conditions.Select(glossary.DescribeCondition));

    /// <summary>An outcome as a fact; <paramref name="superType"/>: see <see cref="DescribeDamageSource"/>.</summary>
    public static string DescribeOutcome(this KeywordGlossary glossary, Outcome outcome, Optional<DamageType> superType) =>
        outcome.Match(
            grant => grant.Amount.Match(
                fraction => $"+{fraction.Amount.FormatPercent()} {grant.To.DescribeAbility()} energy",
                _ => $"refills {grant.To.DescribeAbility()}"),
            convert => $"spends {glossary.DescribeStatus(convert.Consumed)} → +{convert.PerStack.FormatPercent()} {convert.To.DescribeAbility()} energy per stack",
            apply => DescribeBuff(glossary, apply),
            remove => $"consumes {glossary.DescribeStatus(remove.Status)}",
            debuff => $"{glossary.DescribeStatus(debuff.Status)} target"
                + glossary.ReadShownDuration(debuff.Status, debuff.Duration).Match(d => $" ({d.Value.FormatSeconds()})", _ => ""),
            spawn => (spawn.Count > 1 ? $"{spawn.Count}× " : "") + glossary.DescribePickup(spawn.Pickup),
            summon => (summon.Count > 1 ? $"{summon.Count}× " : "") + glossary.DescribeSummon(summon.Summon),
            strike => $"{glossary.DescribeStatus(strike.Via)} strike" + (strike.Hit == HitOutcome.Kill ? " (kills)" : ""),
            modify => DescribeDamageChange(glossary, modify.Against, modify.Change, superType),
            heal => (heal.IncludesAllies ? "heals you and allies" : "heals you")
                + (heal.Amount is GameValue.Unknown ? "" : $" {heal.Amount.FormatPercent()}"),
            reset => $"refills {reset.Which.DescribeAbility()}",
            collect => $"collects {glossary.DescribePickup(collect.Pickup)} from the ground",
            least => least.Amount.Match(
                fraction => $"+{fraction.Amount.FormatPercent()} energy to your least-charged ability",
                _ => "refills your least-charged ability"));

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
            .Select(mention => NarrowOutcomeToCopies(mention.Outcome, mention.Copies))
            .Select(outcome => new OutcomePhrase(
                glossary.DescribeOutcome(outcome, superType),
                outcome,
                outcome is Outcome.GrantEnergy { Amount: EnergyGrant.Fraction fraction } grant
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

    private static Outcome NarrowOutcomeToCopies(Outcome outcome, int copies) =>
        outcome switch
        {
            Outcome.GrantEnergy { Amount: EnergyGrant.Fraction fraction } grant =>
                grant with { Amount = new EnergyGrant.Fraction(fraction.Amount.NarrowToCopies(copies)) },
            Outcome.GrantLeastChargedEnergy { Amount: EnergyGrant.Fraction fraction } least =>
                least with { Amount = new EnergyGrant.Fraction(fraction.Amount.NarrowToCopies(copies)) },
            Outcome.ConvertStacksToEnergy convert => convert with { PerStack = convert.PerStack.NarrowToCopies(copies) },
            Outcome.ModifyDamage modify => modify with { Change = modify.Change.NarrowToCopies(copies) },
            Outcome.RestoreHealth heal => heal with { Amount = heal.Amount.NarrowToCopies(copies) },
            _ => outcome,
        };

    /// <summary>"+1 Bolt Charge" for a status that stacks (the grant, a fact), "Amplified (15s)" otherwise.</summary>
    private static string DescribeBuff(KeywordGlossary glossary, Outcome.ApplyBuff apply)
    {
        var name = glossary.DescribeStatus(apply.Status);
        var suffix = glossary.ReadShownDuration(apply.Status, apply.Duration).Match(d => $" ({d.Value.FormatSeconds()})", _ => "");
        return glossary.IsStacking(apply.Status) ? $"+{apply.Stacks.Value} {name}{suffix}" : $"{name}{suffix}";
    }

    /// <summary>The duration an outcome shows: the rule's, else the glossary's — a fact, never counted down.</summary>
    private static Optional<Seconds> ReadShownDuration(this KeywordGlossary glossary, StatusId status, Optional<Seconds> stated) =>
        stated.IsSome()
            ? stated
            : glossary.Statuses.TryGetValue(status, out var definition) ? definition.Duration : Optional.None<Seconds>();

    /// <summary>
    /// A status's facts: "up to x10" (the build's cap: Charged Up raises Armor Charge's), "15s" from the glossary — shown,
    /// never counted (ADRs D1); none when the glossary gives neither.
    /// </summary>
    public static Optional<string> DescribeStatusFacts(this ValidatedBuild build, StatusId status)
    {
        var duration = build.Catalog.Glossary.Statuses.TryGetValue(status, out var definition) ? definition.Duration : Optional.None<Seconds>();
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

    /// <summary>A buff on you as the state shows it: "Bolt Charge", or "Bolt Charge (at max)" once the player declared it.</summary>
    public static string DescribeActiveBuff(this KeywordGlossary glossary, ActiveBuff buff) =>
        glossary.DescribeStatus(buff.Status) + (buff.AtMax ? " (at max)" : "");

    /// <summary>
    /// A passive as a fact, its values narrowed to the <paramref name="copies"/> equipped (Weapon Surge with two copies:
    /// "+17% Arc weapon damage"), as a rule's outcomes are; <paramref name="superType"/>: see <see cref="DescribeDamageSource"/>.
    /// </summary>
    public static string DescribePassive(this KeywordGlossary glossary, Passive passive, int copies, Optional<DamageType> superType) =>
        NarrowPassiveToCopies(passive, copies).Match(
            extra => $"+{extra.Extra.Value} {glossary.DescribeStatus(extra.Status)} per gain",
            charges => $"+{charges.Extra} {charges.Ability.DescribeAbility()} charge",
            modify => DescribeDamageChange(glossary, modify.Against, modify.Change, superType),
            resist => DescribeResistance(resist),
            weapons => $"{DescribeArchetypes(weapons.Archetypes)}: "
                + string.Join(", ", weapons.Changes.Select(change => DescribeStatChange(change))),
            raise => $"{glossary.DescribeStatus(raise.Status)} up to x{raise.Max.FormatNumber()}",
            stat => $"{FormatSignedNumber(stat.Change)} {stat.Stat}",
            combatant => $"{combatant.Amount.FormatPercent()} damage resistance against {combatant.Against.DescribeCombatantDamage(superType)}");

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
    private static Passive NarrowPassiveToCopies(Passive passive, int copies) =>
        passive switch
        {
            Passive.ModifyDamage modify => modify with { Change = modify.Change.NarrowToCopies(copies) },
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

    /// <summary>"Linear Fusion Rifles, Fusion Rifles and Heat Weapons": weapon archetypes as the game names them, plural.</summary>
    public static string DescribeArchetypes(ImmutableArray<string> archetypes) =>
        JoinWithAnd(archetypes.Select(archetype => DescribeArchetype(archetype) + "s").ToImmutableArray());

    /// <summary>"+? Handling" (the source gives no number: "?", never 0), else "Reload 20%".</summary>
    private static string DescribeStatChange(WeaponStatChange change) =>
        change.Change is GameValue.Unknown
            ? $"+{Unknown} {ToTitleWords(change.Stat)}"
            : $"{ToTitleWords(change.Stat)} {change.Change.FormatPercent()}";

    /// <summary>"a", "a and b", "a, b and c".</summary>
    private static string JoinWithAnd(ImmutableArray<string> parts) =>
        parts.Length <= 1 ? string.Concat(parts) : $"{string.Join(", ", parts[..^1])} and {parts[^1]}";

    // ── events and actions ─────────────────────────────────────────────────────

    public static string DescribeEvent(this KeywordGlossary glossary, GameEvent gameEvent, Build build) =>
        gameEvent.Match(
            cast => DescribeCast(cast.Kind, cast.Input),
            damaged => $"{glossary.DescribeOrigin(damaged.Origin, build)} hit{DescribeOnTarget(DescribeTargetPhrase(glossary, damaged.TargetHas))}",
            killed => $"{glossary.DescribeOrigin(killed.Origin, build)} kill{DescribeOnTarget(DescribeTargetPhrase(glossary, killed.TargetHas))}",
            struck => $"{glossary.DescribeOrigin(struck.Origin, build)} {(struck.Hit == HitOutcome.Kill ? "killed" : "hit")} {DescribeEnemies(struck.Targets)}",
            pickedUp => $"Picked up {glossary.DescribePickup(pickedUp.Pickup)}",
            gained => $"{glossary.DescribeStatus(gained.Status)} gained",
            maxed => $"Max {glossary.DescribeStatus(maxed.Status)}",
            _ => Finisher,
            _ => CriticalHealth);

    /// <summary>
    /// "Grenade (kill)", "Grenade (kill 3)", "Festival Flight (hit 5)", "Class ability", "Pick up Orb of Power",
    /// "Bolt Charge at max (x10)", "Amplified ends", "New pack", "Critical health", "Finisher".
    /// </summary>
    public static string DescribeAction(this ValidatedBuild build, PlayerAction action) =>
        action.Match(
            cast => $"{Capitalize(cast.Kind.ToAbilityKind().DescribeAbilityUse(cast.Input))} ({DescribeHit(cast.Hit, cast.Targets)})",
            use => use.Input switch
            {
                AbilityInput.Air => "Class ability (in the air)",
                _ => "Class ability",
            },
            fire => $"{DescribeWeapon(build.Build, fire.Slot, fire.Slot.ToString())} ({DescribeHit(fire.Hit, fire.Targets)})",
            collect => $"Pick up {build.Catalog.Glossary.DescribePickup(collect.Pickup)}",
            declare => build.DescribeDeclaration(declare.Declaration),
            _ => Finisher);

    /// <summary>"Armor Charge at max (x4)": the max names the build's cap (Charged Up raises it), a fact the player declares reached.</summary>
    private static string DescribeDeclaration(this ValidatedBuild build, StateDeclaration declaration) =>
        declaration.Match(
            max => $"{build.Catalog.Glossary.DescribeStatus(max.Status)} at max" + build.DescribeStackCap(max.Status).Match(cap => $" ({cap.Value})", _ => ""),
            end => $"{build.Catalog.Glossary.DescribeStatus(end.Status)} ends",
            _ => "New pack",
            _ => CriticalHealth);

    /// <summary>"kill", "hit", and with more than one target "kill 3", "hit 5".</summary>
    private static string DescribeHit(HitOutcome hit, TargetCount targets) =>
        (hit == HitOutcome.Kill ? "kill" : "hit") + (targets.Value > 1 ? $" {targets.Value}" : "");

    /// <summary>"1 enemy", "3 enemies".</summary>
    private static string DescribeEnemies(TargetCount targets) =>
        targets.Value == 1 ? "1 enemy" : $"{targets.Value} enemies";

    /// <summary>
    /// The action's token, the same everywhere (CLI, loop files, the web): <c>grenade:kill</c>, <c>grenade:kill:3</c>,
    /// <c>class</c>, <c>kinetic</c>, <c>kinetic:hit:5</c>, <c>pickup:orb-of-power</c>, <c>max:bolt-charge</c>,
    /// <c>end:amplified</c>, <c>pack:new</c>, <c>health:critical</c>, <c>finisher</c>. The target count is written only when it is more than one; every
    /// token round-trips.
    /// </summary>
    public static string ToActionToken(this PlayerAction action) =>
        action.Match(
            cast => cast.Kind.ToString().ToLowerInvariant() + ToInputSuffix(cast.Input) + ToHitSuffix(cast.Hit, cast.Targets),
            use => "class" + ToInputSuffix(use.Input),
            fire => fire.Slot.ToString().ToLowerInvariant() + ToHitSuffix(fire.Hit, fire.Targets),
            collect => $"pickup:{collect.Pickup}",
            declare => declare.Declaration.Match(
                max => $"max:{max.Status}",
                end => $"end:{end.Status}",
                _ => "pack:new",
                _ => "health:critical"),
            _ => "finisher");

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

    /// <summary>"Reaper ← #1": a need and where it comes from, without the elements (comparisons).</summary>
    public static string DescribeNeedBriefly(this StepNeed need) =>
        $"{need.What} {need.Provider.DescribeProvider()}";

    /// <summary>The verdict on a loop's order (docs/loop-format.md, "Analysis").</summary>
    public static string DescribeVerdict(this LoopVerdict verdict) =>
        verdict.Match(
            _ => "The loop has no steps.",
            repeats => repeats.FirstPassBlocked.Match(
                blocked => $"✓ Repeats — on the first pass, #{blocked.Value.StepIndex + 1} ({blocked.Value.Label}) can't happen yet: {blocked.Value.Reason}",
                _ => "✓ Repeats — each pass ends with what the next one needs"),
            breaks => $"✗ Breaks at #{breaks.Blocked.StepIndex + 1} ({breaks.Blocked.Label}): {breaks.Blocked.Reason}");

    /// <summary>"✓ Repeats", "✗ Breaks at #3", "No steps" — the verdict in a word or two (comparison rows, badges).</summary>
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
