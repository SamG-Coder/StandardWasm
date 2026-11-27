# Stage 132 · Primitive unmanaged pointers

Run `npm run demo -- stage-132-unmanaged-pointers` on Windows with the .NET 10 SDK, the x86
runtime, Node and Microsoft Edge. Build the self-hosted compiler with `npm test` first.

The fixture is an ordinary netstandard2.0 library built with `AllowUnsafeBlocks`. Its `Reader`
has the pointer core of `System.IO.UnmanagedMemoryStream`: a `byte*` field, a length, a capacity
and a position. `ReadByte` uses `ldind.u1` and `WriteByte` uses `stind.i1`. The library has no
`fixed`, no `stackalloc` and no pinning, because the memory belongs to the host.

The x86 CLR oracle runs each kernel over `Marshal.AllocHGlobal` memory. Pointer arguments are
offsets into one 256-byte image. The self-hosted compiler compiles each kernel in Node and in
Edge. The runner places the same bytes in a fresh `WebAssembly.Memory` at address 1024, which it
lends as `standardwasm:resources.memory`, and passes the same offsets. Each case must return the
CLR's value and leave memory identical to the CLR's image, with nothing written outside it.

The kernels cover:

- summing bytes through the reader, including an empty range
- writing through the reader and reading back with a `byte*` cursor
- `sbyte` sign extension
- `ushort` and `short` elements with explicit scaling
- `int` reads with `long` writes and reads
- `float` reads stored as `double`
- a pointer returned from a helper, pointer difference and null
- pointer round-trips through `IntPtr.ToPointer()` on a local and
  `UIntPtr.ToPointer()` on an argument, including a null pointer
- a null dereference, which raises `NullReferenceException` and is caught
- the reader's argument checks, and writes past capacity

A Wasm-only case reads past the end of the lent memory. That must be an engine trap and not a
typed managed exception. The CLR would raise an access violation. The design is decision 0060.
