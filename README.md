[![](https://img.shields.io/nuget/v/Soenneker.Extensions.Stream.svg?style=for-the-badge)](https://www.nuget.org/packages/Soenneker.Extensions.Stream/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.extensions.stream/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.extensions.stream/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/Soenneker.Extensions.Stream.svg?style=for-the-badge)](https://www.nuget.org/packages/Soenneker.Extensions.Stream/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.extensions.stream/codeql.yml?label=CodeQL&style=for-the-badge)](https://github.com/soenneker/soenneker.extensions.stream/actions/workflows/codeql.yml)

# ![](https://user-images.githubusercontent.com/4441470/224455560-91ed3ee7-f510-4041-a8d2-3fc093025112.png) Soenneker.Extensions.Stream
A collection of helpful Stream extension methods.

## Installation

```bash
dotnet add package Soenneker.Extensions.Stream
```

## Quick start

```csharp
using Soenneker.Extensions.Stream;

// Given an existing System.IO.Stream named stream:
var result = stream.ToStart();
```

## Common operations

- `ToStart()` - Sets a seekable stream's position to zero and returns the same stream for fluent use.
- `ToStrSync()` - Reads the remaining stream contents as UTF-8.
- `ToStr()` - Reads the remaining stream contents as UTF-8.
- `TryGetTotalLength()` - Returns `Length` for a seekable stream, or `null` when seeking or length access is unavailable.
