using Dunet;

namespace Loopsmith.Web.E2E.Infrastructure.Vocabulary;

/// <summary>An armor mod, as a step card names what it fired. A case is added when a test needs it.</summary>
[Union]
public partial record ArmorMod
{
    public partial record StrandSiphon;
    public partial record EmergencyReinforcement;
    public partial record HealthFont;
    public partial record ArcResistance;
    public partial record SolarResistance;
    public partial record VoidResistance;
    public partial record StasisResistance;
    public partial record StrandResistance;
    public partial record MeleeDamageResistance;
    public partial record SniperDamageResistance;
    public partial record BolsteringDetonation;
    public partial record FocusingStrike;
    public partial record MomentumTransfer;
    public partial record MeleeKickstart;
    public partial record Firepower;
    public partial record HeavyHanded;
    public partial record ShieldBreakCharge;
    public partial record Absolution;
    public partial record BetterAlready;
    public partial record Innervation;
    public partial record Insulation;
    public partial record Invigoration;
    public partial record OrbsOfRestoration;
    public partial record StacksOnStacks;
    public partial record WeaponsFont;
    public partial record ArcWeaponSurge;
    public partial record SolarWeaponSurge;
    public partial record VoidWeaponSurge;
    public partial record StasisWeaponSurge;
    public partial record StrandWeaponSurge;
    public partial record KineticWeaponSurge;

    /// <summary>What the UI shows that no case names yet: a failing assertion names it as the UI shows it.</summary>
    public partial record Unlisted(string Shown);
}

/// <summary>What the UI shows for each <see cref="ArmorMod"/>: the only place those strings live.</summary>
public static class ArmorModShowing
{
    /// <summary>What the UI shows for <paramref name="value"/>.</summary>
    public static string ToShownText(this ArmorMod value) =>
        value.Match(
            strandSiphon => "Strand Siphon",
            emergencyReinforcement => "Emergency Reinforcement",
            healthFont => "Health Font",
            arcResistance => "Arc Resistance",
            solarResistance => "Solar Resistance",
            voidResistance => "Void Resistance",
            stasisResistance => "Stasis Resistance",
            strandResistance => "Strand Resistance",
            meleeDamageResistance => "Melee Damage Resistance",
            sniperDamageResistance => "Sniper Damage Resistance",
            bolsteringDetonation => "Bolstering Detonation",
            focusingStrike => "Focusing Strike",
            momentumTransfer => "Momentum Transfer",
            meleeKickstart => "Melee Kickstart",
            firepower => "Firepower",
            heavyHanded => "Heavy Handed",
            shieldBreakCharge => "Shield Break Charge",
            absolution => "Absolution",
            betterAlready => "Better Already",
            innervation => "Innervation",
            insulation => "Insulation",
            invigoration => "Invigoration",
            orbsOfRestoration => "Orbs of Restoration",
            stacksOnStacks => "Stacks on Stacks",
            weaponsFont => "Weapons Font",
            arcWeaponSurge => "Arc Weapon Surge",
            solarWeaponSurge => "Solar Weapon Surge",
            voidWeaponSurge => "Void Weapon Surge",
            stasisWeaponSurge => "Stasis Weapon Surge",
            strandWeaponSurge => "Strand Weapon Surge",
            kineticWeaponSurge => "Kinetic Weapon Surge",
            unlisted => unlisted.Shown);
}
