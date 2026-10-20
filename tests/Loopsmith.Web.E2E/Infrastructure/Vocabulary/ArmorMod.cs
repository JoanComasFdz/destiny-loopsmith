using Dunet;

namespace Loopsmith.Web.E2E.Infrastructure.Vocabulary;

/// <summary>An armor mod, as a step card names what it fired. A case is added when a test needs it.</summary>
[Union]
public partial record ArmorMod
{
    public partial record ElementalCharge;
    public partial record GrenadeKickstart;
    public partial record Bomber;
    public partial record Reaper;
    public partial record HarmonicSiphon;
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
    public partial record ImpactInduction;
    public partial record AshesToAssets;
    public partial record Recuperation;
    public partial record PowerfulAttraction;
    public partial record HarmonicResistance;
    public partial record ConcussiveDampener;
    public partial record ChargedUp;

    /// <summary>What the UI shows that no case names yet: a failing assertion names it as the UI shows it.</summary>
    public partial record Unlisted(string Shown);
}

/// <summary>What the UI shows for each <see cref="ArmorMod"/>: the only place those strings live.</summary>
public static class ArmorModShowing
{
    /// <summary>What the UI shows for <paramref name="value"/>.</summary>
    public static string ToShownText(this ArmorMod value) =>
        value.Match(
            elementalCharge => "Elemental Charge",
            grenadeKickstart => "Grenade Kickstart",
            bomber => "Bomber",
            reaper => "Reaper",
            harmonicSiphon => "Harmonic Siphon",
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
            impactInduction => "Impact Induction",
            ashesToAssets => "Ashes to Assets",
            recuperation => "Recuperation",
            powerfulAttraction => "Powerful Attraction",
            harmonicResistance => "Harmonic Resistance",
            concussiveDampener => "Concussive Dampener",
            chargedUp => "Charged Up",
            unlisted => unlisted.Shown);
}
