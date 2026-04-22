using Soenneker.Benchmarking.Extensions.Summary;
using Soenneker.Tests.Benchmark;

namespace Soenneker.Extensions.Stream.Tests.Benchmarks;

public class BenchmarkRunner : BenchmarkTest
{
    public BenchmarkRunner() : base()
    {
    }

    //[Test]
    public async System.Threading.Tasks.ValueTask ToStr()
    {
        var summary = BenchmarkDotNet.Running.BenchmarkRunner.Run<ToStrBenchmarks>(DefaultConf);

        await summary.OutputSummaryToLog(OutputHelper, CancellationToken);
    }
}