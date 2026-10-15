using System.Text.Json;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend;

/// <summary>
/// How a page object reads what a script it runs in the page returns (camelCase JSON) into its records: Playwright only
/// builds types with a parameterless constructor, so the script's result comes back as JSON and is read here.
/// </summary>
public static class ShownJson
{
    /// <summary>camelCase names, read case-insensitively.</summary>
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web);
}
