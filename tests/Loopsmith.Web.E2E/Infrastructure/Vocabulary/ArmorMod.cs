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
            unlisted => unlisted.Shown);
}
