# Stage 366 · Benchmark console members

Run `npm run demo -- stage-366-benchmark-console-members` on Windows with the .NET 10 SDK, the x86
.NET 10 runtime, Node and Microsoft Edge. Pass `--no-rebuild` after the runner to reuse
`artifacts/StandardWasm.Compiler.wasm` from the last `npm test`, or set
`STANDARDWASM_COMPILER_PATH` to test a specific self-hosted compiler.

The app is an ordinary .NET 10 console program, published unchanged as a win-x86 apphost. It uses
the platform members that benchmark drivers such as
[How fast is C#?](https://devmobasa.github.io/how-fast-is-csharp/) need (#326). The 32-bit CLR's
native word matches wasm32, so `IntPtr.Size` and `nuint.MaxValue` agree too. For each argument set
(none, and `exit`), the runner checks that the x86 CLR, Node and Edge agree byte for byte on:

- the exit status
- stdout
- stderr

The app covers:

- `ToString()` on every integer type through locals, arguments, init-only struct fields and array
  elements, including both extremes
- `string ==` and `!=`, and `String.Concat` of three, four and an array of strings, including the
  CLR's reference-identity results
- `Char.IsWhiteSpace` over all 65,536 code units
- `Stopwatch.GetTimestamp` and `Frequency`, printed only as properties that hold on every host
- the exact `(ulong)((UInt128)a * b / c)` form, `Math.BigMul`, and the `BitOperations` counts
- `Array.Sort<T>` for 32- and 64-bit integers
- `ldelema` on `uint`, `long`, `ulong` and `byte` elements
- `MemoryMarshal.GetArrayDataReference` with `Unsafe.Add`
- `NativeMemory` allocation, reallocation, alignment, zeroing, `Fill`, `Clear` and `Copy`
- `Unsafe.SkipInit` and `Unsafe.BitCast`
- hardware feature queries: both paths must compute the same value
- typed `catch` of thrown `ArgumentException` family objects
- the `GC` members, `Console.Out.Flush` and `Environment.Exit`, which skips the pending `finally`

[Decision 0104](../../docs/decisions/0104-benchmark-console-members.md) records each lowering and its
CLR-to-Wasm differences. Evidence is written to `artifacts/benchmark-console-members/run-*/results.json`.
