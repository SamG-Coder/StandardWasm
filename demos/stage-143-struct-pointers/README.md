# Stage 143 · Struct pointers and sizeof

Run `npm run demo -- stage-143-struct-pointers` on Windows with the .NET 10 SDK, the x86 runtime,
Node and Microsoft Edge. Build the self-hosted compiler with `npm test` first.

The fixture is an ordinary netstandard2.0 library built with `AllowUnsafeBlocks`. The x86 CLR
oracle runs each kernel over `Marshal.AllocHGlobal` memory. Its 4-byte native size is the wasm32
width. The runner lends a fresh `WebAssembly.Memory`, places the same 256-byte image at address
1024 and passes the same offsets. Each case must return the CLR's value and leave the same memory
image.

The kernels cover:

- `sizeof` of 12 structs:
  - `{byte, long}` and `{byte, double}`, because 8-byte fields align to 8 on x86
  - `{byte, int}` and `{short, byte}`
  - a nested struct
  - `{byte, void*, byte*}`
  - `{char, bool}`
  - an empty struct
  - `{long, byte}`
  - `Rect`
  - an enum field
  - a struct whose only other member is a static field
- `Rect*` reads (`Area`) and read-modify-writes (`Grow`)
- a mixed struct with 8-byte-aligned `long` and `double` fields
- `&r->Bottom` used as an `int*`
- a null `Rect*`, which raises `NullReferenceException` and is caught
- a walk over `rs[i]`, scaled by `sizeof(Rect)`, including an empty walk
- a generic getter returning `T*` from closed receivers for `Rect` and `int`
- a mutating struct method called through a native pointer and from a managed local
  in the same compiled module
- initializing and resetting a local through its original struct slot while
  a pointer still aliases it, with a static field preceding its instance fields

`sizeof` of a `Pack = 1` struct (5 on the x86 CLR) and of a `Size = 12` struct (12) must be the
explicit -2 diagnostic at the `sizeof` instruction. Natural alignment would get them wrong.

The design is decision 0071.
