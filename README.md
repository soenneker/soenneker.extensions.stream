[![](https://img.shields.io/nuget/v/Soenneker.Extensions.Stream.svg?style=for-the-badge)](https://www.nuget.org/packages/Soenneker.Extensions.Stream/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.extensions.stream/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.extensions.stream/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/Soenneker.Extensions.Stream.svg?style=for-the-badge)](https://www.nuget.org/packages/Soenneker.Extensions.Stream/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.extensions.stream/codeql.yml?label=CodeQL&style=for-the-badge)](https://github.com/soenneker/soenneker.extensions.stream/actions/workflows/codeql.yml)

# ![](https://user-images.githubusercontent.com/4441470/224455560-91ed3ee7-f510-4041-a8d2-3fc093025112.png) Soenneker.Extensions.Stream
UTF-8 reading, bounded previews, length inspection, and rewind helpers for `Stream`.

## Installation

```bash
dotnet add package Soenneker.Extensions.Stream
```

## Read the remaining stream

```csharp
using Soenneker.Extensions.Stream;

CancellationToken cancellationToken = default;
await using Stream stream = File.OpenRead("message.txt");
string text = await stream.ToStr(leaveOpen: true, cancellationToken);
```

`ToStr()` and `ToStrSync()` decode from the stream's current position to its end and advance the stream. They use UTF-8 replacement fallback for malformed byte sequences. A UTF-8 BOM is removed only when reading from position zero on a seekable stream; for a non-seekable stream, the reader handles a BOM at the point reading begins.

The default is `leaveOpen: false`, so these methods dispose the stream after reading. Pass `leaveOpen: true` when ownership remains with the caller. The async overload observes cancellation and uses asynchronous disposal when it owns the stream.

## Read a bounded preview

```csharp
await using Stream responseBody = await response.Content.ReadAsStreamAsync(cancellationToken);

(string preview, long? totalLength) = await responseBody.ReadTextUpTo(
    cap: 16 * 1024,
    cancellationToken);
```

`ReadTextUpTo()` reads at most `cap` bytes from the current position and leaves the stream open. `totalLength` is the whole stream length when the stream exposes it, not the remaining length. Because the cap is measured in bytes, the last UTF-8 sequence may be incomplete and decode to the replacement character. Use a parser or an incremental decoder when a syntactically complete text prefix is required.

Temporary byte buffers used by the package are cleared before they are returned to the shared pool.

## Rewind and inspect

```csharp
stream.ToStart();
long? length = StreamExtension.TryGetTotalLength(stream);
```

`ToStart()` rewinds seekable streams and is a no-op for non-seekable streams. `TryGetTotalLength()` does not consume the stream and returns `null` when the stream is non-seekable or its `Length` accessor fails.
