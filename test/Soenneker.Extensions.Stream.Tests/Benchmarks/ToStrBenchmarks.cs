using System.IO;
using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Soenneker.Extensions.Stream.Tests.Benchmarks;

[MemoryDiagnoser]
public class ToStrBenchmarks
{
    private readonly byte[] _data = Encoding.UTF8.GetBytes("234234234");

    [GlobalSetup]
    public void Setup()
    {
    }

    [Benchmark(Baseline = true)]
    public async ValueTask<string> BuiltInAsync()
    {
        using var stream = new MemoryStream(_data);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync().ConfigureAwait(false);
    }

    [Benchmark]
    public string BuiltInSync()
    {
        using var stream = new MemoryStream(_data);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [Benchmark]
    public async ValueTask<string> ToStr()
    {
        using var stream = new MemoryStream(_data);
        return await stream.ToStr();
    }

    [Benchmark]
    public string ToStrSync()
    {
        using var stream = new MemoryStream(_data);
        return stream.ToStrSync();
    }
}