# Stage 367 · Benchmark structure members

Run `npm run demo -- stage-367-benchmark-structure-members` on Windows with the .NET 10 SDK, the
x86 .NET 10 runtime, Node and Microsoft Edge. Pass `--no-rebuild` after the runner to reuse
`artifacts/StandardWasm.Compiler.wasm` from the last `npm test`, or set
`STANDARDWASM_COMPILER_PATH` to test a specific self-hosted compiler.

The app is an ordinary .NET 10 console program, published unchanged as a win-x86 apphost. It uses
the members that the data structures of the
[How fast is C#?](https://devmobasa.github.io/how-fast-is-csharp/) benchmarks need beyond their
drivers (#326). The runner checks that the x86 CLR, Node and Edge agree byte for byte on the exit
status, stdout and stderr.

The app covers:

- **Rank-2 arrays:** `static readonly long[,]` and `int[,]` initializers (with and without
  `InitializeArray`), and element reads with out-of-range rows, columns and negative indices.
- **Math and Buffer:** `Math.Clamp` for 32- and 64-bit integers, including min > max, and
  overlapping `Buffer.MemoryCopy` in both directions plus its size check.
- **Explicit-layout nodes:** an `[StructLayout(LayoutKind.Explicit, Size = 64)]` struct with a
  fixed buffer, reached through a pointer: `sizeof`, field offsets and the raw bytes.
- **Pinning:** `fixed` over arrays with pointer arithmetic on the fixed variable, the same array
  pinned twice, an exception leaving the pinned frame, and an empty array. Also `fixed` over a
  `ReadOnlySpan<byte>` slice of an array, including an empty slice and an invalid one.
- **Array members:** `Array.Fill` (both overloads and their checks), `Array.Resize(ref field, n)`
  (same size keeps the array), `Array.Copy` between `byte[]` fields, `Array.Clone` with
  `castclass`, and `Array.BinarySearch` with duplicates and misses.
- **Native spans:** `Span<uint>(void*, int).Fill` and `Clear`, and a negative length.
- **Small cases:** compound assignments to `bool[]` elements, and an `[InlineArray]` pool of
  structs reached through `Unsafe.Add(ref Unsafe.As<A, E>(ref field), i)`.
- **Vectors:** `Vector512<int>` and `Vector512<long>` loads, stores, comparisons, `ConditionalSelect`,
  `Min`/`Max`, `ExtractMostSignificantBits`, `Widen` with tuple returns, `Shuffle` with
  out-of-range indices, shifts, `Abs`, reinterpretation and `ToScalar`.
- **Narrower vectors:** `Vector256`/`Vector128` halves, `Vector128.GetElement` (and its range
  check), `Vector128.Shuffle`, and vectors stored through `Vector128<int>*`.
- **Guarded intrinsics:** a `Bmi2.IsSupported` path that must compute the same value as its
  fallback.
- **UInt128 tuples:** `UInt128` arithmetic in `(UInt128, int)` tuples, `Array.Sort` over a range of
  them by the tuple's default order, and the sort's argument checks.

[Decision 0105](../../docs/decisions/0105-benchmark-structure-members.md) records each lowering and
its CLR-to-Wasm differences. Evidence is written to
`artifacts/benchmark-structure-members/run-*/results.json`.
