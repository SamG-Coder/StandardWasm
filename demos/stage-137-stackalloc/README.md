# Stage 137 · stackalloc on a shadow stack

Run `npm run demo -- stage-137-stackalloc` on Windows with the .NET 10 SDK, the x86 runtime,
Node and Microsoft Edge. Build the self-hosted compiler with `npm test` first.

The fixture is an ordinary netstandard2.0 library built with `AllowUnsafeBlocks`. It uses
`stackalloc` into `byte*`, `int*` and `long*`. The x86 CLR runs every kernel as the oracle, and its
4-byte native size is the wasm32 width. The self-hosted compiler compiles each kernel in Node and
Edge. Each module imports only the lent `standardwasm:resources.memory`.

The kernels cover:

- byte and int buffers, including an empty and a 4 KiB one
- int buffers that are zero-filled before use
- nested frames that each allocate
- recursion through 21 frames
- 2,000 top-level allocations that must not exhaust the 1 MiB stack
- 100 frames of 64 KiB that throw a `DivideByZeroException` to their caller, which must release
  each frame
- a `[SkipLocalsInit]` method
- `stackalloc int[0x40000000]`, whose size overflows and raises `OverflowException`
- a 600 KiB block, which fits once but not twice

A Wasm-only sequence allocates 600 KiB and then exhausts the stack in a nested frame. That is an
engine trap: the CLR's `StackOverflowException` would end the process. The next host call on the
same instance must find an empty shadow stack. The design is decision 0065.
