# Stage 174 · Inline reference-array constructors

Run `npm run demo -- stage-174-inline-reference-array-constructors` with
the .NET 10 SDK, the x86 Windows runtime, Node, and Microsoft Edge. The runner
uses the same compiled C# assembly in each runtime.

Value constructors can initialize object elements through the compiler-generated
first-element and constant-index helpers for a verified `InlineArray`.
The linker verifies the closed helper types, helper body, and immediate store,
then updates the original constructor snapshot at the corresponding index.

The cases cover all three slots, default and null values, independence after
copying, and a generic inline array closed over `object`. An intervening call
between the helper and its store receives a located diagnostic. Results go
to `artifacts/stage-174-inline-reference-array-constructors/results.json`.
