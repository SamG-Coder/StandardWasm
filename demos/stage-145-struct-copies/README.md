# Stage 145 · Struct copies through pointers

Run `npm run demo -- stage-145-struct-copies` on Windows with the .NET 10 SDK, the x86 runtime,
Node and Microsoft Edge. Build the self-hosted compiler with `npm test` first.

The fixture is an ordinary netstandard2.0 library built with `AllowUnsafeBlocks`. The x86 CLR
oracle runs each kernel over `Marshal.AllocHGlobal` memory. The runner lends a fresh
`WebAssembly.Memory` with the same 256-byte image at address 1024 and passes the same offsets.
Each case must return the CLR's value and leave the same memory image.

The kernels cover:

- `Rect r = *p` (`ldobj`)
- `*p = r` (`stobj`)
- `*p = default` (`initobj` at a native address)
- a swap through two pointers
- a nested struct read, modified as a whole value and written back
- a `dst[i] = src[i]` array copy, including an empty one
- a copy from a null pointer, which raises `NullReferenceException` and is caught

ECMA-335 leaves padding bytes unspecified, so the structs here have no padding. The design is
decision 0073.
