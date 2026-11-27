# Stage 278 · Writable byte spans

The ranged managed array constructor creates an ordinary writable `Span<byte>` over the original byte array. It retains the same owner and checks the requested range. The original `MemoryExtensions.AsSpan<byte>` forwards to that constructor. Span copies, slices and read-only views share the backing data. The typed writable indexer can read and write a byte within the view.

Run `npm run demo -- stage-278-writable-byte-spans`. Set `STANDARDWASM_COMPILER_PATH` for a frozen compiler, and optionally set `STANDARDWASM_MONO_CORELIB` to verify the genuine browser Mono CoreLib body against the Windows x86 oracle. The same ordinary library is compared in Node and Edge for aliasing, unsigned byte values, null and empty spans, and typed range and index exceptions. Native byte pinning and file I/O have separate boundaries.
