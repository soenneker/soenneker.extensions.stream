using System;
using System.Buffers;
using System.Diagnostics.Contracts;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Extensions.Stream;

/// <summary>
/// A collection of helpful Stream extension methods
/// </summary>
public static class StreamExtension
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static System.IO.Stream ToStart(this System.IO.Stream stream)
    {
        if (stream.CanSeek && stream.Position != 0)
            stream.Seek(0, SeekOrigin.Begin);

        return stream;
    }

    /// <summary>Reads entire stream as UTF-8.</summary>
    [Pure, MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static string ToStrSync(this System.IO.Stream stream, bool leaveOpen = false)
    {
        // Fast path: MemoryStream with exposed buffer => zero-copy into string
        if (stream is MemoryStream ms && ms.TryGetBuffer(out var seg))
        {
            var span = seg.AsSpan();
            if (HasUtf8Bom(span)) span = span[3..];
            return Encoding.UTF8.GetString(span);
        }

        // If we can know the remaining length, read exactly once then decode
        if (stream.CanSeek)
        {
            var remaining = stream.Length - stream.Position;
            if (remaining <= 0) return string.Empty;

            // Cap at int.MaxValue (string length limitation)
            var len = remaining > int.MaxValue ? int.MaxValue : (int) remaining;

            var rented = ArrayPool<byte>.Shared.Rent(len);
            try
            {
                var readTotal = 0;
                while (readTotal < len)
                {
                    var read = stream.Read(rented, readTotal, len - readTotal);
                    if (read == 0) break;
                    readTotal += read;
                }

                var span = rented.AsSpan(0, readTotal);
                if (HasUtf8Bom(span)) 
                    span = span[3..];

                return Encoding.UTF8.GetString(span);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
                if (!leaveOpen) stream.Dispose();
            }
        }

        // Generic path: non-seekable streams (network, pipes, etc.)
        using var reader = new StreamReader(stream, encoding: Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 16 * 1024,
            leaveOpen: leaveOpen);
        return reader.ReadToEnd();
    }

    /// <summary>Reads entire stream as UTF-8.</summary>
    [Pure, MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static async ValueTask<string> ToStr(this System.IO.Stream stream, bool leaveOpen = false, CancellationToken cancellationToken = default)
    {
        if (stream is MemoryStream ms && ms.TryGetBuffer(out var seg))
        {
            var span = seg.AsSpan();
            if (HasUtf8Bom(span)) 
                span = span[3..];

            return Encoding.UTF8.GetString(span);
        }

        if (stream.CanSeek)
        {
            var remaining = stream.Length - stream.Position;
            if (remaining <= 0)
                return string.Empty;

            var len = remaining > int.MaxValue ? int.MaxValue : (int) remaining;

            var rented = ArrayPool<byte>.Shared.Rent(len);
            try
            {
                var readTotal = 0;
                while (readTotal < len)
                {
                    var read = await stream.ReadAsync(rented.AsMemory(readTotal, len - readTotal), cancellationToken).ConfigureAwait(false);

                    if (read == 0) 
                        break;

                    readTotal += read;
                }

                var span = rented.AsSpan(0, readTotal);
                if (HasUtf8Bom(span)) 
                    span = span[3..];

                return Encoding.UTF8.GetString(span);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);

                if (!leaveOpen) 
                    await stream.DisposeAsync().ConfigureAwait(false);
            }
        }

        using var reader = new StreamReader(stream, encoding: Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 16 * 1024,
            leaveOpen: leaveOpen);
        return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool HasUtf8Bom(ReadOnlySpan<byte> span) => span.Length >= 3 && span[0] == 0xEF && span[1] == 0xBB && span[2] == 0xBF;

    /// <summary>
    /// Reads up to <paramref name="cap"/> bytes from <paramref name="s"/> and decodes as UTF-8.
    /// Returns the decoded text and total stream length if available (null if not).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static async ValueTask<(string text, long? totalLength)> ReadTextUpTo(this System.IO.Stream s, int cap,
        CancellationToken cancellationToken = default)
    {
        if (cap <= 0)
            return (string.Empty, TryGetTotalLength(s));

        var rented = ArrayPool<byte>.Shared.Rent(cap);
        try
        {
            var totalRead = 0;

            // We reuse this Memory once and only slice by offset, which is cheaper.
            var mem = rented.AsMemory(0, cap);

            while (totalRead < cap)
            {
                var read = await s.ReadAsync(mem.Slice(totalRead), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                    break;

                totalRead += read;
            }

            // Decode only what we read; Span overload avoids an extra copy/range cost.
            var text = totalRead == 0 ? string.Empty : Encoding.UTF8.GetString(rented.AsSpan(0, totalRead));

            return (text, TryGetTotalLength(s));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long? TryGetTotalLength(System.IO.Stream s)
    {
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