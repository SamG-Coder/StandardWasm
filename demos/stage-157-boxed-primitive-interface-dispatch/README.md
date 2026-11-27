# Stage 157 · Boxed primitive interface dispatch

Build the self-hosted compiler with `npm test`, then run
`npm run demo -- stage-157-boxed-primitive-interface-dispatch` on Windows
with the .NET 10 SDK, the x86 runtime, Node and Microsoft Edge.

The runner compiles ordinary C# calls through CoreLib's `ISpanFormattable`.
It compares Node and Edge against the x86 CLR using the same fixture DLL.
The cases verify exact interface membership, identity of a successful cast,
and both buffer sizes for the original explicit `Char.TryFormat` body.
A managed reference implementation exercises the same call site and checks
its `out` value and writes to the slice.

Both `Int32` cases have CLR answers. Until the generic numeric formatting
body lowers, the verifier accepts only the located diagnostic at the call
to `Number.TryFormatInt32<char>` in the original `Int32.TryFormat`. Once
that body compiles, the verifier requires the CLR answer.

See decision 0084 and issue #272.
