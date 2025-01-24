using System.Diagnostics.Contracts;
using System.IO;
using System.Text;

namespace Soenneker.Extensions.Stream;

/// <summary>
/// A collection of helpful Stream extension methods
/// </summary>
public static class StreamExtension
{
    /// <summary>
    /// Moves the position of the stream to the beginning. Returns the same stream for fluency.
    /// </summary>
    /// <remarks>Shorthand for <code>Stream.Seek(0, SeekOrigin.Begin)</code></remarks>
    /// <param name="stream">The stream to move to the start.</param>
    public static System.IO.Stream ToStart(this System.IO.Stream stream)
    {
        stream.Seek(0, SeekOrigin.Begin);
        return stream;
    }

    /// <summary>
    /// Reads the entire content of the stream as a string without closing the stream or resetting its position.
    /// </summary>
    [Pure]
    public static string ToStr(this System.IO.Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);
        return reader.ReadToEnd();
    }
}