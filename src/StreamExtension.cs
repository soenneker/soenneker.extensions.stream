using System.Diagnostics.Contracts;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Extensions.Stream;

/// <summary>
/// A collection of helpful Stream extension methods
/// </summary>
public static class StreamExtension
{
    /// <summary>
    /// Moves the position of the stream to the beginning.
    /// </summary>
    /// <remarks>Shorthand for <code>Stream.Seek(0, SeekOrigin.Begin)</code></remarks>
    /// <param name="stream">The stream to move to the start.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ToStart(this System.IO.Stream stream)
    {
        stream.Seek(0, SeekOrigin.Begin);
    }

    /// <summary>
    /// Does not close the stream. Reads via a StreamReader. Does not reset the position of the stream once done
    /// </summary>
    [Pure]
    public static Task<string> ToStr(this System.IO.Stream value, CancellationToken cancellationToken = default)
    {
        var reader = new StreamReader(value);
        return reader.ReadToEndAsync(cancellationToken);
    }
}