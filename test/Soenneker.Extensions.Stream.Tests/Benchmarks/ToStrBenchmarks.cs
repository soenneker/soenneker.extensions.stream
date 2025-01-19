using System.IO;
using System.Text;
using BenchmarkDotNet.Attributes;

namespace Soenneker.Extensions.Stream.Tests.Benchmarks;

[MemoryDiagnoser]
public class ToStrBenchmarks
{
    private MemoryStream _value = null!;

    [GlobalSetup]
    public void Setup()
    {
        _value = new MemoryStream(Encoding.UTF8.GetBytes("234234234"));
    }

    [Benchmark(Baseline =true)]
    public string ToStr()
    {
        return _value.ToStr();
    }
}