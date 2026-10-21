using Loopsmith.E2ETools.Core;
using Loopsmith.Functional;

namespace Loopsmith.E2ETools.Tests;

/// <summary>When Home shows in a recorded run's video, from ffmpeg's scene scores around the time the test noted.</summary>
public sealed class HomeFindingTests
{
    private const string Probe = """
        [Parsed_metadata_1 @ 0x1] frame:0    pts:0       pts_time:0
        [Parsed_metadata_1 @ 0x1] lavfi.scene_score=0.000000
        [Parsed_metadata_1 @ 0x1] frame:1    pts:40      pts_time:0.04
        [Parsed_metadata_1 @ 0x1] lavfi.scene_score=0.012000
        [Parsed_metadata_1 @ 0x1] frame:2    pts:1240    pts_time:1.24
        [Parsed_metadata_1 @ 0x1] lavfi.scene_score=0.310000
        [Parsed_metadata_1 @ 0x1] frame:3    pts:1280    pts_time:1.28
        [Parsed_metadata_1 @ 0x1] lavfi.scene_score=0.090000
        """;

    [Fact]
    public void Home_starts_a_tenth_of_a_second_into_the_first_big_change_after_the_probe_start() =>
        Assert.Equal("3.340", HomeFinding.FormatSeconds(HomeFinding.FindHome(3.0, Probe)));

    [Fact]
    public void Without_a_big_change_it_is_two_tenths_after_the_noted_time() =>
        Assert.Equal("3.200", HomeFinding.FormatSeconds(HomeFinding.FindHome(3.0, "pts_time:0.5\nscene_score=0.010000\n")));

    [Fact]
    public void The_probe_starts_a_second_before_the_noted_time_never_before_the_video()
    {
        Assert.Equal(2.5, HomeFinding.FindProbeStart(3.5));
        Assert.Equal(0.0, HomeFinding.FindProbeStart(0.4));
        Assert.Equal("1.340", HomeFinding.FormatSeconds(HomeFinding.FindHome(0.0, Probe)));
    }

    [Fact]
    public void The_probe_asks_ffmpeg_for_four_seconds_of_scene_scores_from_its_start() =>
        Assert.Equal(
            ["-nostdin", "-hide_banner", "-ss", "2.500", "-t", "4", "-i", "out/.videos/a.webm",
             "-vf", "select='gte(scene,0)',metadata=print:key=lavfi.scene_score", "-f", "null", "-"],
            HomeFinding.WriteProbeArguments("out/.videos/a.webm", 2.5));

    [Fact]
    public void A_runs_video_is_next_to_its_record_and_its_line_gives_what_record_sh_reads()
    {
        var record = new RunRecord("Starting", "StartFromScratchTests", "Opens", "Opens", Optional.None<string>(), "mobile", "opens-mobile", Optional.Some(3.0), true);

        Assert.Equal("out/.videos/opens-mobile.webm", HomeFinding.FindVideo("out/.videos/opens-mobile.json"));
        Assert.Equal("Starting opens-mobile mobile 3.340", HomeFinding.WriteHomeLine(record, HomeFinding.FindHome(3.0, Probe)));
    }
}
