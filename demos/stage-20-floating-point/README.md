# Stage 20 · Floating point

Run `npm run demo -- stage-20-floating-point` from the root after `npm ci`. Expected output is `KahanSum(0.1, 10) = 1`. Plain summation of the same ten terms gives 0.9999999999999999.

Reals.cs maps `double` to Wasm `f64` and `float` to `f32`. It covers `ldc.r4`/`ldc.r8` (including -0.0, the infinities, NaN, subnormals, and 2^-958, whose .NET 10 `"R"` string does not round-trip), add/sub/mul/div/neg, ordered and unordered comparisons in compare and branch form, and the conversions `conv.r4`, `conv.r8`, `conv.r.un`, `conv.i4`, `conv.u4`, `conv.i8`, `conv.u8` and `conv.u1`. The ordinary numeric code is naive and Kahan summation, a Horner polynomial, a bounded Newton square root, `float` lerp and a NaN-transparent clamp. CheckedReals.cs holds the rejection fixtures: `double % double` and `checked((int)double)` must be diagnosed with the method and IL offset.

`npm test` compares every export with the same DLL on the CLR using `Object.is`, so NaN equals NaN and -0 differs from +0. It crosses 34 special doubles with each other and 16 special floats with each other, and adds 300 random bit patterns per type.

| Semantics | CLR (.NET 10) | Wasm lowering |
| --- | --- | --- |
| IEEE add/sub/mul/div, `float` rounded to single precision | per operation | `f32.*` / `f64.*` |
| `-x` | sign flip (`-0.0` from `0.0`) | `fNN.neg`, not `0 - x` |
| `clt.un`, `bge.un` and other `.un` forms | true if unordered (NaN) | negated complementary ordered compare, e.g. `!(a >= b)` |
| float → int (`conv.i4/u4/i8/u8`) | saturating, NaN → 0 (.NET 9+) | `trunc_sat` (Wasm 2.0 non-trapping conversions) |
| `(byte)double` (`conv.u1`) | saturate to int32, keep the low 8 bits (`300.7` → 44) | `i32.trunc_sat_f64_s` then `& 255` |
| `(float)ulong` (`conv.r.un; conv.r4`) | one rounding (RyuJIT fuses the pair) | fused to `f32.convert_i64_u` |
| `(float)(double)ulong` | two roundings | `f64.convert_i64_u`, then `f32.demote_f64` |

Limitations: float `%` needs an exact fmod (diagnosed). `Math.*` and other BCL calls are not linked. There are no float arrays, `conv.ovf.*` is diagnosed, and the IL `switch` instruction is not lowered yet. NaN payloads are not compared, because `Object.is` treats all NaNs as equal. The conversion results depend on .NET 9+ saturating semantics. Older runtimes returned platform-specific values.
