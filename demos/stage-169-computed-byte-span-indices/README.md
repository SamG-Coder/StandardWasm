# Stage 169 · Computed byte span indices

Run `npm run demo -- stage-169-computed-byte-span-indices` with the .NET 10
SDK, the x86 Windows runtime, Node, and Microsoft Edge. The runner uses the
same compiled .NET Standard 2.1 fixture on the CLR and both Wasm engines.

A local or argument `ReadOnlySpan<byte>` can supply its descriptor to a
verified index read while a straight-line expression computes the index.
The expression still runs in its original order. The read uses the slice
offset and length, and the view observes writes to its backing array.

The cases cover bitwise and shift operators, an unsigned native byte
load, offset and negative indices, and a typed bounds failure. Calls and
branch entries inside the index expression retain their compiler
diagnostics. Results go to
`artifacts/stage-169-computed-byte-span-indices/results.json`.
