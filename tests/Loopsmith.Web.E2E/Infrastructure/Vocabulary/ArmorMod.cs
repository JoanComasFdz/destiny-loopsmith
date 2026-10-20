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
    public partial record ArcSiphon;
    public partial record SolarSiphon;
    public partial record VoidSiphon;
    public partial record StasisSiphon;
    public partial record KineticSiphon;
    public partial record ArcStasisDualSiphon;
    public partial record ArcStrandSiphonCombo;
    public partial record KineticChargeSiphon;
    public partial record KineticEntropySiphon;
    public partial record KineticThermalSiphon;
    public partial record SolarStrandDualSiphon;
    public partial record ThermodynamicSiphon;
    public partial record VoidStasisDualSiphon;
    public partial record VoidStrandDualSiphon;
    public partial record Dynamo;
    public partial record HandsOn;
    public partial record RadiantLight;
    public partial record SuperFont;

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
            arcSiphon => "Arc Siphon",
            solarSiphon => "Solar Siphon",
            voidSiphon => "Void Siphon",
            stasisSiphon => "Stasis Siphon",
            kineticSiphon => "Kinetic Siphon",
            arcStasisDualSiphon => "Arc/Stasis Dual Siphon",
            arcStrandSiphonCombo => "Arc/Strand Siphon Combo",
            kineticChargeSiphon => "Kinetic Charge Siphon",
            kineticEntropySiphon => "Kinetic Entropy Siphon",
            kineticThermalSiphon => "Kinetic Thermal Siphon",
            solarStrandDualSiphon => "Solar/Strand Dual Siphon",
            thermodynamicSiphon => "Thermodynamic Siphon",
            voidStasisDualSiphon => "Void/Stasis Dual Siphon",
            voidStrandDualSiphon => "Void/Strand Dual Siphon",
            dynamo => "Dynamo",
            handsOn => "Hands-On",
            radiantLight => "Radiant Light",
            superFont => "Super Font",
            unlisted => unlisted.Shown);
}
