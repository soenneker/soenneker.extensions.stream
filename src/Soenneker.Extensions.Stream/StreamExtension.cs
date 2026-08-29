using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;
using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Extensions.Stream;

/// <summary>
/// A collection of helpful Stream extension methods.
/// </summary>
public static class StreamExtension
{
    private const int _defaultByteChunk = 32 * 1024; // 32KB
    private const int _defaultCharChunk = 16 * 1024; // 16K chars

    private const int _singleDecodeThreshold = 1024 * 1024; // 1MB

    /// <summary>
    /// Sets a seekable stream's position to zero and returns the same stream.
    /// </summary>
    /// <param name="stream">The stream to reposition or inspect.</param>
    /// <returns>The same stream instance, positioned at zero.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static System.IO.Stream ToStart(this System.IO.Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (stream.CanSeek && stream.Position != 0)
            stream.Position = 0;

        return stream;
    }

    /// <summary>
    /// Reads the remaining stream contents as UTF-8.
    /// </summary>
    /// <returns>Reads the remaining stream contents as UTF-8.</returns>
    public static string ToStrSync(this System.IO.Stream stream, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (stream is MemoryStream ms && ms.TryGetBuffer(out ArraySegment<byte> segment))
        {
            try
            {
                return ReadMemoryStreamUtf8(ms, segment);
            }
            finally
            {
                if (!leaveOpen)
                    ms.Dispose();
            }
        }

        if (TryGetRemaining(stream, out long position, out long remaining))
        {
            try
            {
                if (remaining <= 0)
                    return string.Empty;

                if (remaining <= _singleDecodeThreshold)
                    return ReadSmallSeekableUtf8Sync(stream, checked((int)remaining), stripBom: position == 0);

                using var largeReader = new StreamReader(stream, encoding: Encoding.UTF8, detectEncodingFromByteOrderMarks: position == 0,
                    bufferSize: _defaultByteChunk, leaveOpen: true);
                return largeReader.ReadToEnd();
            }
            finally
            {
                if (!leaveOpen)
                    stream.Dispose();
            }
        }

        using var reader = new StreamReader(stream, encoding: Encoding.UTF8, detectEncodingFromByteOrderMarks: true,
            bufferSize: _defaultByteChunk, leaveOpen: leaveOpen);

        return reader.ReadToEnd();
    }

    /// <summary>
    /// Reads the remaining stream contents as UTF-8.
    /// </summary>
    /// <returns>Reads the remaining stream contents as UTF-8.</returns>
    public static ValueTask<string> ToStr(this System.IO.Stream stream, bool leaveOpen = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (stream is MemoryStream ms && ms.TryGetBuffer(out ArraySegment<byte> segment))
        {
            try
            {
                return System.Threading.Tasks.ValueTask.FromResult(ReadMemoryStreamUtf8(ms, segment));
            }
            finally
            {
                if (!leaveOpen)
                    ms.Dispose();
            }
        }

        return ToStrCore(stream, leaveOpen, cancellationToken);
    }

    private static async ValueTask<string> ToStrCore(System.IO.Stream stream, bool leaveOpen,
        CancellationToken cancellationToken)
    {
        if (TryGetRemaining(stream, out long position, out long remaining))
        {
            try
            {
                if (remaining <= 0)
                    return string.Empty;

                if (remaining <= _singleDecodeThreshold)
                {
                    return await ReadSmallSeekableUtf8Async(stream, checked((int)remaining), stripBom: position == 0,
                        cancellationToken).NoSync();
                }

                using var largeReader = new StreamReader(stream, encoding: Encoding.UTF8, detectEncodingFromByteOrderMarks: position == 0,
                    bufferSize: _defaultByteChunk, leaveOpen: true);
                return await largeReader.ReadToEndAsync(cancellationToken).NoSync();
            }
            finally
            {
                if (!leaveOpen)
                    await stream.DisposeAsync().NoSync();
            }
        }

        using var reader = new StreamReader(stream, encoding: Encoding.UTF8, detectEncodingFromByteOrderMarks: true,
            bufferSize: _defaultByteChunk, leaveOpen: leaveOpen);

        return await reader.ReadToEndAsync(cancellationToken).NoSync();
    }

    private static string ReadMemoryStreamUtf8(MemoryStream ms, ArraySegment<byte> segment)
    {
        long positionLong = ms.Position;
        long remainingLong = ms.Length - positionLong;

        if (remainingLong <= 0)
            return string.Empty;

        var position = checked((int)positionLong);
        var remaining = checked((int)remainingLong);

        ReadOnlySpan<byte> span = segment.AsSpan(position, remaining);

        if (position == 0 && HasUtf8Bom(span))
            span = span[3..];

        string result = Encoding.UTF8.GetString(span);

        // Match normal stream-consumption behavior.
        ms.Position = ms.Length;

        return result;
    }

    private static string ReadSmallSeekableUtf8Sync(System.IO.Stream stream, int count, bool stripBom)
    {
        byte[] rented = ArrayPool<byte>.Shared.Rent(count);

        try
        {
            Span<byte> buffer = rented.AsSpan(0, count);
            int read = stream.ReadAtLeast(buffer, count, throwOnEndOfStream: false);

            if (read <= 0)
                return string.Empty;

            ReadOnlySpan<byte> span = rented.AsSpan(0, read);

            if (stripBom && HasUtf8Bom(span))
                span = span[3..];

            return Encoding.UTF8.GetString(span);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private static async ValueTask<string> ReadSmallSeekableUtf8Async(System.IO.Stream stream, int count, bool stripBom,
        CancellationToken cancellationToken)
    {
        byte[] rented = ArrayPool<byte>.Shared.Rent(count);

        try
        {
            int read = await stream.ReadAtLeastAsync(
                rented.AsMemory(0, count), count, throwOnEndOfStream: false, cancellationToken).NoSync();

            if (read <= 0)
                return string.Empty;

            ReadOnlySpan<byte> span = rented.AsSpan(0, read);

            if (stripBom && HasUtf8Bom(span))
                span = span[3..];

            return Encoding.UTF8.GetString(span);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    /// <summary>
    /// Reads up to <paramref name="cap"/> bytes from <paramref name="stream"/> and decodes them as UTF-8.
    /// Returns the decoded text and total stream length if available; otherwise null.
    /// </summary>
    /// <returns>Reads up to <paramref name="cap"/> bytes from <paramref name="stream"/> and decodes them as UTF-8. Returns the decoded text and total stream length if available; otherwise null.</returns>
    public static async ValueTask<(string text, long? totalLength)> ReadTextUpTo(this System.IO.Stream stream, int cap,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        long? totalLength = TryGetTotalLength(stream);

        if (cap <= 0)
            return (string.Empty, totalLength);

        long startPosition = 0;
        var canSeek = stream.CanSeek;

        if (canSeek)
        {
            try
            {
                startPosition = stream.Position;
            }
            catch
            {
                canSeek = false;
            }
        }

        byte[] rented = ArrayPool<byte>.Shared.Rent(cap);

        try
        {
            int totalRead = await stream.ReadAtLeastAsync(
                rented.AsMemory(0, cap), cap, throwOnEndOfStream: false, cancellationToken).NoSync();

            if (totalRead <= 0)
                return (string.Empty, totalLength);

            ReadOnlySpan<byte> span = rented.AsSpan(0, totalRead);

            if ((!canSeek || startPosition == 0) && HasUtf8Bom(span))
                span = span[3..];

            return (Encoding.UTF8.GetString(span), totalLength);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    /// <summary>
    /// Returns a stream's length when it is available without consuming the stream.
    /// </summary>
    /// <param name="stream">The stream to reposition or inspect.</param>
    /// <returns>The stream length, or null when unavailable.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long? TryGetTotalLength(System.IO.Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanSeek)
            return null;

        try
        {
            return stream.Length;
        }
        catch
        {
            return null;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryGetRemaining(System.IO.Stream stream, out long position, out long remaining)
    {
        position = 0;
        remaining = 0;

        if (!stream.CanSeek)
            return false;

        try
        {
            position = stream.Position;
            remaining = stream.Length - position;
            return true;
        }
        catch
        {
            return false;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool HasUtf8Bom(ReadOnlySpan<byte> span)
    {
        return span.Length >= 3 && span[0] == 0xEF && span[1] == 0xBB && span[2] == 0xBF;
    }
}
