using System.IO;

namespace Soenneker.Extensions.Stream;

public static class StreamExtension
{
    /// <summary>
    /// Shorthand for <code>Stream.Seek(0, SeekOrigin.Begin)</code>
    /// </summary>
    public static void ToStart(this System.IO.Stream stream)
    {
        stream.Seek(0, SeekOrigin.Begin);
    }
}