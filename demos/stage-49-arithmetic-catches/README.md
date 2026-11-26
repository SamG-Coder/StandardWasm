# Stage 49 · Ordered arithmetic catches

Run `npm run demo -- stage-49-arithmetic-catches`. The generated
`SpecificThenBase(18, 0)` returns `-101` from the specific catch. The
same method returns `-102` for `(int.MinValue, -1)` and `9` for `(18, 2)`.

Portable Wasm uses distinct tags for managed overflow and integer division
by zero. Sibling catches on one protected range select the first clause
that matches the tag, including a catch for the platform
`System.ArithmeticException` base type. An unmatched tag escapes
with its type. Array bounds failures still surface as Wasm traps.

`npm test` compares the same .NET Standard DLL with the CLR in Node
and Edge and replays the cases with a generated compiler. Handler use of
the exception object, nested protected regions, and filters and finally
blocks require further lowering.
