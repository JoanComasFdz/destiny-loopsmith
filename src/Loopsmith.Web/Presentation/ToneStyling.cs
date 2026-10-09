using Loopsmith.Core.Domain;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Web.Presentation;

/// <summary>Pure: the core's tones and affinities → CSS classes (the palette lives in app.css).</summary>
public static class ToneStyling
{
    public static string ToToneClass(Tone tone) => $"tone-{tone.ToString().ToLowerInvariant()}";

    public static string ToAffinityClass(Affinity affinity) => $"aff-{affinity.ToString().ToLowerInvariant()}";

    public static string ToAffinityClass(Subclass subclass) => ToAffinityClass(subclass.ToAffinity());

    public static string ToAffinityClass(DamageType type) => ToAffinityClass(type.ToAffinity());
}
