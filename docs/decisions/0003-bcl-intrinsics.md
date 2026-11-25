# 0003 · Lower exactly-specified BCL methods to Wasm instructions

Status: accepted for the host compiler (#58). The portable compiler in `src/` can adopt the same table later.

The README requires every supported .NET Standard API to have a compatible implementation in the module or a documented host import. Some BCL methods need neither. Their .NET behavior on every input is already a Wasm instruction, or a short guarded sequence. The host compiler maps those calls through a curated table in `Compiler/BclIntrinsics.cs` instead of linking the BCL.

## Rule

A method enters the table only if its result is bit-identical on every input to the runtime the CLR oracle uses (.NET 10). NaN payloads are excluded, because the oracle compares NaNs as equal. Where .NET throws, the lowering traps before producing a value, and the harness reports the CLR exception type next to the trap. Everything else stays an `Unlinked call` diagnostic with the method and IL offset until an implementation is compiled into the module. "Close enough" approximations are not allowed. For example, `Math.Pow`, `Math.Exp` and `Math.Sin` have no Wasm instruction and depend on the libm implementation, so they stay unlinked.

A method's full signature does not identify its assembly, so a match also requires the declaring scope to be a platform BCL assembly. The check uses the assembly name plus its Microsoft public key token: `netstandard` (cc7b13ffcd2ddd51), `mscorlib` (b77a5c561934e089), `System.Runtime` and `System.Runtime.Extensions` (b03f5f7f11d50a3a), and `System.Private.CoreLib` (7cec85d7bea7798e). The call must also be static. A `System.Math` defined in the input assembly compiles as an ordinary method. One defined in any other dependency stays an unlinked call. Codex's review of #63 showed why: a third-party `System.Math.Sqrt` reached through an extern alias returns 11 on the CLR for `Compute(9)`, and lowering it to `f64.sqrt` gave 3. Stage 29 keeps that two-DLL case as a regression test (`identity/`).

## Current table

| .NET (netstandard2.0 surface) | Wasm | Basis |
| --- | --- | --- |
| `Math.Sqrt(double)` | `f64.sqrt` | IEEE 754 correctly rounded square root |
| `Math.Abs(double)`, `Math.Abs(float)` | `f64.abs`, `f32.abs` | clears the sign bit |
| `Math.Floor`, `Ceiling`, `Truncate(double)` | `f64.floor`, `ceil`, `trunc` | exact. -0 is preserved: `Ceiling(-0.5)` is -0 |
| `Math.Round(double)` | `f64.nearest` | MidpointRounding.ToEven = roundTiesToEven, -0 preserved |
| `Math.Max`, `Min(double/float)` | `fNN.max`, `fNN.min` | .NET Core 3.0+ return NaN when either operand is NaN and order -0 < +0. .NET Framework did not, so this entry assumes .NET Core 3.0+ |
| `Math.Max`, `Min(int/uint/long/ulong)` | `select` + `gt/lt` with the parameter's signedness | exact |
| `Math.Abs(int/long)` | trap if MinValue, then `select` | .NET throws OverflowException for MinValue |

## Evidence

Stage 29 compares every entry with the same DLL on the CLR, using `Object.is`, over ±0, ±∞, NaN, halfway values, subnormals, integer edges and random bit patterns. Mutation checks confirm that a naive compare-and-select `Math.Max` (wrong for ±0 and NaN) and a missing `Abs` overflow guard both fail the suite.
