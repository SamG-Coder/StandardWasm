# Stage 173 · Managed array clearing

Run `npm run demo -- stage-173-array-clear` with the Windows x86 .NET 10
runtime, Node, and Edge. Set `STANDARDWASM_COMPILER_PATH` to verify a frozen
compiler. Evidence is written to `artifacts/stage-173-array-clear/results.json`.

The compiler recognizes the original CoreLib `System.Array.Clear` overloads
when analysis proves the concrete zero-based vector at the call site. It
retains the managed caller and lowers CoreCLR's method-table and raw storage
operations at the platform boundary. Clearing a plain vector uses the
appropriate Wasm default value. Clearing an allocated aggregate slot resets
its current snapshot. The slot's stable cell and owner remain available to
interior references, and independent snapshots retain their values.

Both engines match the Windows x86 CLR for primitive/reference defaults,
signed and unsigned packed and wide enums,
range preservation, live scalar/reference/aggregate and projected aliases,
an independent snapshot, and typed null and range errors. The .NET 10 fixture
exercises the whole-array overload. A linked .NET Standard 2.0 assembly
exercises range clearing on a packed vector. Calls whose concrete vector
cannot be proven remain diagnosed, including unresolved `System.Array`
representations and array shapes outside the compiler's supported vectors.
Typed failures use the compiler's fixed Wasm exception tags. These tags carry
no CLR exception object or `ParamName`.
[CoreLib Array.Clear](https://github.com/dotnet/runtime/blob/v10.0.10/src/libraries/System.Private.CoreLib/src/System/Array.cs#L584-L634)
