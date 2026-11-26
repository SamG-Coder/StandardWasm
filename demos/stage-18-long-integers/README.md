# Stage 18 · 64-bit integers

Run `npm run demo -- stage-18-long-integers` from the root after `npm ci`. Expected output is `SumOfCubes(1000) = 250500250000`. The sum passes int32 at n = 304, so the loop has to run in int64.

Wide.cs exercises `long` and `ulong` parameters, locals and returns. It covers `ldc.i8`, wrapping add/sub/mul, signed and unsigned division and remainder, shifts with masked int32 counts, negate and complement, and signed and unsigned comparisons in both `cgt`/`clt` and branch form. It also covers `brtrue`/`brfalse` on int64, the `conv.i8`/`conv.u8` widenings and the `conv.i4`/`conv.u1` narrowings. Ordinary uses are a 64-bit FNV-1a hash, a sum of cubes that eventually wraps, and a modular multiply by doubling. `Countdown` recurses a million times through `return_call` with i64 parameters. CheckedWide.cs checks that `checked(long + 1)` returns in-range values and raises a managed overflow for `long.MaxValue`.

`npm test` compares every export with the same DLL on the CLR. It crosses 17 boundaries (long.MinValue/MaxValue, ±2^31, 2^32, uint.MaxValue and others) with each other, applies shift counts 0, 1, 31, 32, 33, 63, 64, 65 and -1, and adds 200 seeded random pairs.

| Input class | CLR | Wasm (V8 message) |
| --- | --- | --- |
| Any zero divisor | `DivideByZeroException` | trap `divide by zero` / `remainder by zero` |
| `long.MinValue / -1` | `OverflowException` | trap `divide result unrepresentable` |
| `long.MinValue % -1` | `OverflowException` | trap `unreachable` from the compiler's explicit guard. Plain `i64.rem_s` would return 0. |
| All other inputs | value | identical value |

JavaScript boundary: Wasm i64 values cross as BigInt. Inputs are wrapped to 64 bits on entry, and `ulong` results read back as the signed BigInt with the same bits (`BigInt.asUintN(64, x)` recovers the unsigned value). The oracle encodes 64-bit values as `{"i64": "<signed decimal>"}` because JSON numbers are exact only to 2^53.

Limitations: no `long[]` arrays, no checked 64-bit arithmetic or `conv.ovf.*` (diagnosed), and no floating point. Traps are not typed CLR exceptions.
