using System.IO;
using System.Text;
using AwesomeAssertions;
using Soenneker.Extensions.Task;
using Xunit;

namespace Soenneker.Extensions.Stream.Tests;

public class StreamExtensionTests
{
    [Fact]
    public async System.Threading.Tasks.ValueTask ToStr_should_all_be_equivalent()
    {
        var data = Encoding.UTF8.GetBytes("234234234");

        using var stream1 = new MemoryStream(data);
        using var reader1 = new StreamReader(stream1);
        var result1 = await reader1.ReadToEndAsync().NoSync();

        using var stream2 = new MemoryStream(data);
        using var reader2 = new StreamReader(stream2);
        var result2 = reader2.ReadToEnd();

        using var stream3 = new MemoryStream(data);
        var result3 = await stream3.ToStr();

        using var stream4 = new MemoryStream(data);
        var result4 = stream4.ToStrSync();

        result1.Should().Be(result2).And.Be(result3).And.Be(result4);
    }
}
