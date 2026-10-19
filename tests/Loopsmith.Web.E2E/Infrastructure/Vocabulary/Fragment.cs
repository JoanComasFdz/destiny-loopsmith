using Dunet;

namespace Loopsmith.Web.E2E.Infrastructure.Vocabulary;

/// <summary>A subclass's fragment, as the Subclass box shows it and a step card names what it fired. A case is added when a test needs it.</summary>
[Union]
public partial record Fragment
{
    public partial record SparkOfShock;
    public partial record SparkOfVolts;
    public partial record SparkOfAmplitude;
    public partial record SparkOfBeacons;
    public partial record SparkOfBrilliance;
    public partial record SparkOfDischarge;
    public partial record SparkOfFrequency;
    public partial record SparkOfIons;
    public partial record SparkOfResistance;
    public partial record SparkOfInstinct;
    public partial record SparkOfRecharge;
    public partial record SparkOfMomentum;
    public partial record EchoOfVigilance;

    /// <summary>What the UI shows that no case names yet: a failing assertion names it as the UI shows it.</summary>
    public partial record Unlisted(string Shown);
}

/// <summary>What the UI shows for each <see cref="Fragment"/>: the only place those strings live.</summary>
public static class FragmentShowing
{
    private static readonly Fragment[] Listed =
        [
            new Fragment.SparkOfShock(), new Fragment.SparkOfVolts(), new Fragment.SparkOfAmplitude(), new Fragment.SparkOfBeacons(),
            new Fragment.SparkOfBrilliance(), new Fragment.SparkOfDischarge(), new Fragment.SparkOfFrequency(), new Fragment.SparkOfIons(),
            new Fragment.SparkOfResistance(), new Fragment.SparkOfInstinct(), new Fragment.SparkOfRecharge(), new Fragment.SparkOfMomentum(),
            new Fragment.EchoOfVigilance(),
        ];

    /// <summary>What the UI shows for <paramref name="value"/>.</summary>
    public static string ToShownText(this Fragment value) =>
        value.Match(
            sparkOfShock => "Spark of Shock",
            sparkOfVolts => "Spark of Volts",
            sparkOfAmplitude => "Spark of Amplitude",
            sparkOfBeacons => "Spark of Beacons",
            sparkOfBrilliance => "Spark of Brilliance",
            sparkOfDischarge => "Spark of Discharge",
            sparkOfFrequency => "Spark of Frequency",
            sparkOfIons => "Spark of Ions",
            sparkOfResistance => "Spark of Resistance",
            sparkOfInstinct => "Spark of Instinct",
            sparkOfRecharge => "Spark of Recharge",
            sparkOfMomentum => "Spark of Momentum",
            echoOfVigilance => "Echo of Vigilance",
            unlisted => unlisted.Shown);

    /// <summary>The case the UI's <paramref name="shown"/> text names, or <see cref="Fragment.Unlisted"/>.</summary>
    public static Fragment ReadFragment(string shown) =>
        Listed.FirstOrDefault(value => value.ToShownText() == shown) ?? new Fragment.Unlisted(shown);
}
