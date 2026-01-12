using System;
using System.Buffers;
using System.Diagnostics.Contracts;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;

namespace Soenneker.Extensions.Stream;

/// <summary>
/// A collection of helpful Stream extension methods
/// </summary>
public static class StreamExtension
{
    private const int _defaultByteChunk = 32 * 1024; // 32KB
    private const int _defaultCharChunk = 16 * 1024; // 16K chars

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static System.IO.Stream ToStart(this System.IO.Stream stream)
    {
        if (stream is null)
            throw new ArgumentNullException(nameof(stream));

        if (stream.CanSeek && stream.Position != 0)
        {
            // Slightly cheaper than Seek for many Stream types (e.g., MemoryStream).
            stream.Position = 0;
        }

        return stream;
    }

    /// <summary>Reads entire stream as UTF-8.</summary>
    [Pure, MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static string ToStrSync(this System.IO.Stream stream, bool leaveOpen = false)
    {
        if (stream is null)
            throw new ArgumentNullException(nameof(stream));

        // Fast path: MemoryStream with exposed buffer => single decode (no intermediate copy)
        if (stream is MemoryStream ms && ms.TryGetBuffer(out var seg))
        {
            try
            {
                var pos = (int)ms.Position;
                var remaining = (int)Math.Max(0, ms.Length - ms.Position);
                if (remaining == 0)
                    return string.Empty;

                ReadOnlySpan<byte> span = seg.AsSpan(pos, remaining);

                // Only strip BOM if we are at the beginning of the stream.
                if (pos == 0 && HasUtf8Bom(span))
                    span = span.Slice(3);

                return Encoding.UTF8.GetString(span);
            }
            finally
            {
                if (!leaveOpen)
                    ms.Dispose();
            }
        }

        // Seekable streams: decode incrementally to avoid renting massive buffers.
        if (stream.CanSeek)
        {
            try
            {
                var remainingLong = stream.Length - stream.Position;
                if (remainingLong <= 0)
                    return string.Empty;

                return ReadAllUtf8Sync(stream, stripBom: stream.Position == 0);
            }
            finally
            {
                if (!leaveOpen)
                    stream.Dispose();
            }
        }

        // Generic path: non-seekable streams (network, pipes, etc.)
        using var reader = new StreamReader(stream, encoding: Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 16 * 1024,
            leaveOpen: leaveOpen);

        var result = reader.ReadToEnd();

        if (!leaveOpen)
            stream.Dispose();

        return result;
    }

    /// <summary>Reads entire stream as UTF-8.</summary>
    [Pure, MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static async ValueTask<string> ToStr(this System.IO.Stream stream, bool leaveOpen = false, CancellationToken cancellationToken = default)
    {
        if (stream is null)
            throw new ArgumentNullException(nameof(stream));

        // Fast path: MemoryStream with exposed buffer => single decode (no intermediate copy)
        if (stream is MemoryStream ms && ms.TryGetBuffer(out var seg))
        {
            try
            {
                var pos = (int)ms.Position;
                var remaining = (int)Math.Max(0, ms.Length - ms.Position);
                if (remaining == 0)
                    return string.Empty;

                ReadOnlySpan<byte> span = seg.AsSpan(pos, remaining);

                if (pos == 0 && HasUtf8Bom(span))
                    span = span.Slice(3);

                return Encoding.UTF8.GetString(span);
            }
            finally
            {
                if (!leaveOpen)
                    await ms.DisposeAsync()
                            .NoSync();
            }
        }

        // Seekable streams: decode incrementally to avoid renting massive buffers.
        if (stream.CanSeek)
        {
            try
            {
                var remainingLong = stream.Length - stream.Position;
                if (remainingLong <= 0)
                    return string.Empty;

                return await ReadAllUtf8Async(stream, stripBom: stream.Position == 0, cancellationToken)
                    .NoSync();
            }
            finally
            {
                if (!leaveOpen)
                    await stream.DisposeAsync()
                                .NoSync();
            }
        }

        using var reader = new StreamReader(stream, encoding: Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 16 * 1024,
            leaveOpen: leaveOpen);

        var result = await reader.ReadToEndAsync(cancellationToken)
                                 .NoSync();

        if (!leaveOpen)
            await stream.DisposeAsync()
                        .NoSync();

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool HasUtf8Bom(ReadOnlySpan<byte> span) => span.Length >= 3 && span[0] == 0xEF && span[1] == 0xBB && span[2] == 0xBF;

    private static string ReadAllUtf8Sync(System.IO.Stream stream, bool stripBom)
    {
        // Decoder lets us translate byte chunks -> chars without creating intermediate strings.
        var decoder = Encoding.UTF8.GetDecoder();

        var bytes = ArrayPool<byte>.Shared.Rent(_defaultByteChunk);
        var chars = ArrayPool<char>.Shared.Rent(_defaultCharChunk);

        try
        {
            var sb = new StringBuilder(capacity: 1024);

            var firstChunk = true;

            while (true)
            {
                var read = stream.Read(bytes, 0, bytes.Length);
                if (read <= 0)
                    break;

                ReadOnlySpan<byte> span = bytes.AsSpan(0, read);

                if (firstChunk)
                {
                    firstChunk = false;

                    if (stripBom && HasUtf8Bom(span))
                        span = span.Slice(3);
                }

                while (!span.IsEmpty)
                {
                    decoder.Convert(span, chars, flush: false, out var bytesUsed, out var charsUsed, out _);

                    if (charsUsed > 0)
                        sb.Append(chars, 0, charsUsed);

                    span = span.Slice(bytesUsed);
                }
            }

            // Flush any remaining decoder state.
            decoder.Convert(ReadOnlySpan<byte>.Empty, chars, flush: true, out _, out var finalChars, out _);
            if (finalChars > 0)
                sb.Append(chars, 0, finalChars);

            return sb.ToString();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bytes);
            ArrayPool<char>.Shared.Return(chars);
        }
    }

    private static async ValueTask<string> ReadAllUtf8Async(System.IO.Stream stream, bool stripBom, CancellationToken cancellationToken)
    {
        var decoder = Encoding.UTF8.GetDecoder();

        var bytes = ArrayPool<byte>.Shared.Rent(_defaultByteChunk);
        var chars = ArrayPool<char>.Shared.Rent(_defaultCharChunk);

        try
        {
            var sb = new StringBuilder(capacity: 1024);

            var firstChunk = true;

            while (true)
            {
                var read = await stream.ReadAsync(bytes.AsMemory(0, bytes.Length), cancellationToken)
                                       .NoSync();
                if (read <= 0)
                    break;

                ReadOnlySpan<byte> span = bytes.AsSpan(0, read);

                if (firstChunk)
                {
                    firstChunk = false;

                    if (stripBom && HasUtf8Bom(span))
                        span = span.Slice(3);
                }

                while (!span.IsEmpty)
                {
                    decoder.Convert(span, chars, flush: false, out var bytesUsed, out var charsUsed, out _);

                    if (charsUsed > 0)
                        sb.Append(chars, 0, charsUsed);

                    span = span.Slice(bytesUsed);
                }
            }

            decoder.Convert(ReadOnlySpan<byte>.Empty, chars, flush: true, out _, out var finalChars, out _);
            if (finalChars > 0)
                sb.Append(chars, 0, finalChars);

            return sb.ToString();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bytes);
            ArrayPool<char>.Shared.Return(chars);
        }
    }

    /// <summary>
    /// Reads up to <paramref name="cap"/> bytes from <paramref name="s"/> and decodes as UTF-8.
    /// Returns the decoded text and total stream length if available (null if not).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static async ValueTask<(string text, long? totalLength)> ReadTextUpTo(this System.IO.Stream s, int cap,
        CancellationToken cancellationToken = default)
    {
        if (s is null)
            throw new ArgumentNullException(nameof(s));

        var totalLen = TryGetTotalLength(s);

        if (cap <= 0)
            return (string.Empty, totalLen);

        // Capture whether we started at position 0 (for BOM stripping).
        long startPos = 0;
        var canSeek = s.CanSeek;
        if (canSeek)
        {
            try
            {
                startPos = s.Position;
            }
            catch
            {
                canSeek = false;
            }
        }

        var rented = ArrayPool<byte>.Shared.Rent(cap);
        try
        {
            var totalRead = 0;
            var mem = rented.AsMemory(0, cap);

            while (totalRead < cap)
            {
                var read = await s.ReadAsync(mem.Slice(totalRead), cancellationToken)
                                  .NoSync();
                if (read == 0)
                    break;

                totalRead += read;
            }

            if (totalRead == 0)
                return (string.Empty, totalLen);

            ReadOnlySpan<byte> span = rented.AsSpan(0, totalRead);

            // Only strip UTF-8 BOM if we started at the beginning.
            if ((!canSeek || startPos == 0) && HasUtf8Bom(span))
                span = span.Slice(3);

            return (Encoding.UTF8.GetString(span), totalLen);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long? TryGetTotalLength(System.IO.Stream s)
    {
        if (s is null)
            throw new ArgumentNullException(nameof(s));

        if (!s.CanSeek)
            return null;

        try
        {
            return s.Length;
        }
        catch
        {
            return null;
        }
    }
}