using System.Buffers;
using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Text;

namespace Soenneker.Extensions.Stream;

/// <summary>
/// A collection of helpful Stream extension methods
/// </summary>
public static class StreamExtension
{
    private static readonly UTF8Encoding SUtf8NoBomNoThrow
        = new(encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: false);

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
        // Special fast path for MemoryStream
        if (stream is MemoryStream ms && ms.TryGetBuffer(out var segment))
        {
            var pos = (int)ms.Position;
            var length = (int)ms.Length;
            var count = length - pos;
            if (count <= 0)
            {
                // Nothing left to read
                return "";
            }
            // Decode straight from the underlying byte[] without copying
            return SUtf8NoBomNoThrow.GetString(segment.Array!, segment.Offset + pos, count);
        }

        // If this stream can seek, try reading everything in one shot
        // (this is often faster than chunking if the entire size is known).
        if (stream.CanSeek)
        {
            var remaining = stream.Length - stream.Position;
            if (remaining <= 0)
            {
                return string.Empty;
            }

            if (remaining > int.MaxValue)
            {
                // This example doesn’t handle streams >2GB in a single read
                // (you could loop or throw).
                throw new NotSupportedException("Stream is too large (>2GB).");
            }

            var size = (int)remaining;
            var buffer = GC.AllocateUninitializedArray<byte>(size);
            // Read exactly size bytes (in rare cases Read might return less, so loop)
            var totalRead = 0;
            while (totalRead < size)
            {
                var read = stream.Read(buffer, totalRead, size - totalRead);
                if (read == 0)
                    break;
                totalRead += read;
            }
            if (totalRead == 0)
            {
                return string.Empty;
            }
            return SUtf8NoBomNoThrow.GetString(buffer, 0, totalRead);
        }

        // Fallback: unknown length (non‐seekable stream). Read in chunks.
        var rented = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            byte[] result = [];
            var totalWritten = 0;

            while (true)
            {
                var read = stream.Read(rented, 0, rented.Length);
                if (read == 0)
                    break;

                var requiredSize = totalWritten + read;
                if (requiredSize > result.Length)
                {
                    // Grow a new array (uninitialized) and copy existing data
                    var newSize = Math.Max(requiredSize, result.Length * 2);
                    var temp = GC.AllocateUninitializedArray<byte>(newSize);
                    Buffer.BlockCopy(result, 0, temp, 0, totalWritten);
                    result = temp;
                }

                Buffer.BlockCopy(rented, 0, result, totalWritten, read);
                totalWritten += read;
            }

            return totalWritten == 0
                ? string.Empty
                : SUtf8NoBomNoThrow.GetString(result, 0, totalWritten);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
}