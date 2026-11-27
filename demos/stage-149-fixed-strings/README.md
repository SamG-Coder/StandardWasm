# Stage 149 · Fixed strings

Run `npm run demo -- stage-149-fixed-strings` on Windows with the .NET 10 SDK, Node and
Microsoft Edge. Build the self-hosted compiler with `npm test` first.

`fixed (char* p = s)` takes the address of a string's UTF-16 code units. Linear memory cannot
address a Wasm GC string. The compiler therefore copies the code units and a NUL terminator
onto the frame's shadow stack (Stage 137), and `p` addresses that copy. A null string stays
address zero. An empty string gets a buffer holding a single NUL.

Roslyn emits a different pinning form for each target, so the same kernels are built twice:

- **netstandard2.0:** a `pinned string` local, `conv.u` and an addition of
  `RuntimeHelpers.OffsetToStringData`.
- **net10.0:** a `pinned char&` local filled by `string.GetPinnableReference()`.

The CLR runs both builds. Node and Edge must return the CLR's value for every case.

The kernels cover:

- the first code unit, a position-weighted sum and the guaranteed terminator
- a managed callee that scans the pointer to its NUL
- two strings pinned at once and compared
- a `stackalloc` buffer filled from a fixed string
- 40 pins in one loop
- a `NullReferenceException` that leaves a fixed statement
- reads after the pointer is passed to a call

The strings are:

- null
- empty
- ASCII
- a CJK unit with a surrogate pair
- a leading NUL
- 300 code units built at run time

Fixing a `char[]` and fixing `&array[0]` remain located diagnostics. The design and its
limitations are in decision 0077.
