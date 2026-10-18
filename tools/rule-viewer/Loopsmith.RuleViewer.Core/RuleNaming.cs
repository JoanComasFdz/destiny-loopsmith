using Loopsmith.Core.Domain;

namespace Loopsmith.RuleViewer.Core;

/// <summary>A rule's full name, where people read it: the rules lanes tool, the e2e rule tests and their recordings (ADRs D40).</summary>
public static class RuleNaming
{
    /// <summary>
    /// Its file's path under the rules folder, without the extension and with dashes for slashes, then its id:
    /// <c>hunter-arc-melees-combination-blow-1</c>. Only its id for an element no rule file holds (a test's).
    /// </summary>
    public static string ToRuleName(this BuildElement element, RuleId rule) =>
        element.RuleFile.Length == 0
            ? rule.Value
            : $"{element.RuleFile[..FindExtensionStart(element.RuleFile)].Replace('/', '-')}-{rule.Value}";

    private static int FindExtensionStart(string file) =>
        file.LastIndexOf('.') is var dot and > 0 && dot > file.LastIndexOf('/') ? dot : file.Length;
}
