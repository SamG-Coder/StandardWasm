# Stage 123 · [SkipLocalsInit] method bodies

Run `npm run demo -- stage-123-skip-locals-init`. The self-hosted compiler
compiles `Kernels.Scalars`, a `[SkipLocalsInit]` method with locals, and the
generated Wasm returns 31 for input 5.

The .NET runtime and Windows Desktop libraries clear `CorILMethod_InitLocals`.
That used to reject any such body with locals, which was the largest single
blocker in the Stage 117 WinForms census (#252). Every local now starts zeroed
or null, and value types start as their zeroed aggregate (decision 0051).

The Verify oracle compares these against the CLR in Node and Edge:

- scalar locals assigned on every path
- a value-type local written field by field and then copied
- locals in nested blocks inside a loop
- reference locals

It also checks that each kernel really clears the flag. `Kernels.Stack` uses
`stackalloc`, which stays an explicit -2 diagnostic.
