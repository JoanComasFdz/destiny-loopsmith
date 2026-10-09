namespace Loopsmith.Core.Tests.Architecture;

internal static class Violations
{
    /// <summary>Fails with the rule and every offending type/member, one per line, sorted.</summary>
    public static void AssertNone(string rule, IEnumerable<string> violations)
    {
        var offenders = violations.Distinct().Order(StringComparer.Ordinal).ToList();
        if (offenders.Count > 0)
        {
            Assert.Fail($"{rule}{Environment.NewLine}{offenders.Count} violation(s):{Environment.NewLine}  "
                + string.Join(Environment.NewLine + "  ", offenders));
        }
    }
}
