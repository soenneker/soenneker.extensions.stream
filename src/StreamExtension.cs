using System.Diagnostics.Contracts;
using System.IO;
using System.Threading.Tasks;

namespace Soenneker.Extensions.Stream;

/// <summary>
/// A collection of helpful Stream extension methods
/// </summary>
public static class StreamExtension
{
    /// <summary>
    /// Shorthand for <code>Stream.Seek(0, SeekOrigin.Begin)</code>
    /// </summary>
    public static void ToStart(this System.IO.Stream stream)
    {
        stream.Seek(0, SeekOrigin.Begin);
    }

    /// <summary>
    /// Does not close the stream. Reads via a StreamReader. Does not reset the position of the stream once done
    /// </summary>
    [Pure]
    public static async ValueTask<string> ToStr(this System.IO.Stream value)
    {
        var reader = new StreamReader(value);
        string result = await reader.ReadToEndAsync();
        return result;
    }
}