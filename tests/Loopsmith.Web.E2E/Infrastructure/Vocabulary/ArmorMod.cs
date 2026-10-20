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
    public partial record BulwarkFinisher;
    public partial record ClassFont;
    public partial record Distribution;
    public partial record EmpoweredFinish;
    public partial record ExplosiveFinisher;
    public partial record HealthyFinisher;
    public partial record OneTwoFinisher;
    public partial record Outreach;
    public partial record ProximityWard;
    public partial record RestorativeFinisher;
    public partial record SnaploadFinisher;
    public partial record SpecialFinisher;
    public partial record UtilityFinisher;
    public partial record UtilityKickstart;
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
            bulwarkFinisher => "Bulwark Finisher",
            classFont => "Class Font",
            distribution => "Distribution",
            empoweredFinish => "Empowered Finish",
            explosiveFinisher => "Explosive Finisher",
            healthyFinisher => "Healthy Finisher",
            oneTwoFinisher => "One-Two Finisher",
            outreach => "Outreach",
            proximityWard => "Proximity Ward",
            restorativeFinisher => "Restorative Finisher",
            snaploadFinisher => "Snapload Finisher",
            specialFinisher => "Special Finisher",
            utilityFinisher => "Utility Finisher",
            utilityKickstart => "Utility Kickstart",
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
