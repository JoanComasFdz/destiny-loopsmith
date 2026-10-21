using Loopsmith.E2ETools.Core;
using Loopsmith.Functional;

namespace Loopsmith.E2ETools.Tests;

/// <summary>record.sh --no-video's table of the unrecorded runs, from the test results.</summary>
public sealed class RunReportingTests
{
    private const string Trx = """
        <?xml version="1.0" encoding="utf-8"?>
        <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
          <Results>
            <UnitTestResult testName="StartFromScratchTests.Opens [📱 Mobile]" duration="00:00:12.3456789" outcome="Passed" />
            <UnitTestResult testName="StartFromScratchTests.Opens [🖥️ Desktop]" duration="00:01:02.05" outcome="Failed" />
            <UnitTestResult testName="ImportTests.Skips" outcome="NotExecuted" />
          </Results>
        </TestRun>
        """;

    [Fact]
    public void Each_run_is_read_with_its_screen_decoded_and_its_time_in_seconds()
    {
        var runs = Assert.IsType<Result<System.Collections.Immutable.ImmutableArray<TestRun>, string>.Ok>(RunReporting.ReadTestRuns(Trx, "web.trx")).Value;

        Assert.Equal(new TestRun("StartFromScratchTests.Opens [📱 Mobile]", "Passed", 12.3456789), runs[0]);
        Assert.Equal(new TestRun("StartFromScratchTests.Opens [🖥️ Desktop]", "Failed", 62.05), runs[1]);
        Assert.Equal(new TestRun("ImportTests.Skips", "NotExecuted", 0), runs[2]);
    }

    [Fact]
    public void The_table_lists_the_runs_by_name_with_their_outcome_and_time()
    {
        var runs = Assert.IsType<Result<System.Collections.Immutable.ImmutableArray<TestRun>, string>.Ok>(RunReporting.ReadTestRuns(Trx, "web.trx")).Value;

        Assert.Equal(
            [
                "",
                "Runs (unrecorded):",
                "  skipped    0.0 s  ImportTests.Skips",
                "   passed   12.3 s  StartFromScratchTests.Opens [📱 Mobile]",
                "   FAILED   62.0 s  StartFromScratchTests.Opens [🖥️ Desktop]",
            ],
            RunReporting.WriteReport(runs));
        Assert.True(RunReporting.HasRunAny(runs));
    }

    [Fact]
    public void Only_skipped_runs_mean_no_scenario_ran() =>
        Assert.False(RunReporting.HasRunAny([new TestRun("A", "NotExecuted", 0)]));

    [Fact]
    public void A_result_without_its_name_is_an_error_naming_the_file() =>
        Assert.StartsWith("web.trx: ", Assert.IsType<Result<System.Collections.Immutable.ImmutableArray<TestRun>, string>.Error>(RunReporting.ReadTestRuns(
            """<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><UnitTestResult outcome="Passed" /></TestRun>""", "web.trx")).Failure);

    [Fact]
    public void A_file_that_isnt_test_results_is_an_error_naming_it() =>
        Assert.Equal("web.trx isn't a test results file.", Assert.IsType<Result<System.Collections.Immutable.ImmutableArray<TestRun>, string>.Error>(RunReporting.ReadTestRuns("not xml", "web.trx")).Failure);
}
