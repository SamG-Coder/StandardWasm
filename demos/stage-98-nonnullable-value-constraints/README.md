# Stage 98 · Non-nullable value constraints

Run `npm run demo -- stage-98-nonnullable-value-constraints` and
`npm test`. The .NET Standard example uses linked scalar and aggregate
arguments under `where T : struct` and returns 24. The CLR oracle checks
the rejection of a reference argument. A standalone constructor constraint
and arbitrary nominal constraints still produce explicit diagnostics.

After building the self-hosted compiler, run
`node demos/stage-98-nonnullable-value-constraints/verify-corelib.mjs`
with the .NET 10 SDK, Node, and Edge. The runner uses the original installed
CoreLib and generates field reads and copies for `Nullable<bool>` and
`Nullable<int>`. It compares results and emitted bytes in both engines.
The generated layout retains a Boolean presence field and the declared
value storage. The original CLR and the generated compiler both reject
`Nullable<int>` as an argument to the non-nullable constraint.

The probe writes its Wasm, disassembly, and results under
`artifacts/corelib-nullable/run-*/`. Set `STANDARDWASM_COMPILER_PATH`
to test a specific generated compiler. Closed generic method execution,
boxing, lifted operators, and managed addresses into fields are separate.
See the [CoreLib Nullable implementation](https://github.com/dotnet/runtime/blob/v10.0.10/src/libraries/System.Private.CoreLib/src/System/Nullable.cs).
