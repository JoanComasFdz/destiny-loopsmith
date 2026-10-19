using Dunet;

namespace Loopsmith.Web.E2E.Infrastructure.Vocabulary;

/// <summary>The rank of the enemy a kill kills, as the trigger palette's "Rank" picker names it (ADRs D45). A case is added when a test needs it.</summary>
[Union]
public partial record EnemyRank
{
    public partial record Minor;
    public partial record Elite;
    public partial record Boss;

    /// <summary>What the UI shows that no case names yet: a failing assertion names it as the UI shows it.</summary>
    public partial record Unlisted(string Shown);
}

/// <summary>What the UI shows for each <see cref="EnemyRank"/>: the only place those strings live.</summary>
public static class EnemyRankShowing
{
    private static readonly EnemyRank[] Listed = [new EnemyRank.Minor(), new EnemyRank.Elite(), new EnemyRank.Boss()];

    /// <summary>What the UI shows for <paramref name="value"/>.</summary>
    public static string ToShownText(this EnemyRank value) =>
        value.Match(
            minor => "Minor",
            elite => "Elite",
            boss => "Boss",
            unlisted => unlisted.Shown);

    /// <summary>The case the UI's <paramref name="shown"/> text names, or <see cref="EnemyRank.Unlisted"/>.</summary>
    public static EnemyRank ReadEnemyRank(string shown) =>
        Listed.FirstOrDefault(value => value.ToShownText() == shown) ?? new EnemyRank.Unlisted(shown);
}
