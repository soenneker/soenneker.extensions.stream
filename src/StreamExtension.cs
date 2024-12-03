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
    public static void ToStart(this System.IO.Stream stream)
    {
        stream.Seek(0, SeekOrigin.Begin);
    }

    /// <summary>
    /// Reads the entire content of the stream as a string without closing the stream or resetting its position.
    /// </summary>
    /// <param name="value">The stream to read from.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous read operation, containing the contents of the stream as a string.</returns>
    [Pure]
    public static Task<string> ToStr(this System.IO.Stream value, CancellationToken cancellationToken = default)
    {
        var reader = new StreamReader(value);
        return reader.ReadToEndAsync(cancellationToken);
    }
}