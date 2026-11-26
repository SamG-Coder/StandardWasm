# Stage 37 · Primitive arrays

Run `npm run demo -- stage-37-primitive-arrays` from the root after `npm ci`. Expected output is `CountPrimes(100) = 25`, from a sieve over a `bool[]`.

Buffers.cs is ordinary array code over every primitive element width: a `bool[]` sieve, `long[]` prefix sums, `double[]` mean and variance, a single-precision `float[]` dot product, a `char[]` Caesar cipher, `uint[]` unsigned maxima, and packed-cell wraparound through `short[]`, `ushort[]`, `char[]` and `sbyte[]`. Each element type maps to a Wasm GC array (`Compiler/ArrayKinds.cs`):

| CLR element | Wasm storage | Loads |
| --- | --- | --- |
| `int`, `uint` | `i32` | `ldelem.i4` / `.u4` |
| `byte`, `sbyte`, `bool` | packed `i8` | `ldelem.u1` → `array.get_u`, `ldelem.i1` → `array.get_s` |
| `short`, `ushort`, `char` | packed `i16` | `ldelem.i2` → `array.get_s`, `ldelem.u2` → `array.get_u` |
| `long`, `ulong` | `i64` | `ldelem.i8` |
| `float` / `double` | `f32` / `f64` | `ldelem.r4` / `.r8` |

Packed stores keep the low bits, as a CLR store into a narrow cell does. Small scalar locals, parameters and returns normalize like `byte` already did: `sbyte`/`short` sign-extend, and `ushort`/`char` mask to 16 bits. `conv.i1`, `conv.i2` and `conv.u2` truncate. From floating point they saturate to int32 first and then keep the low bits, which matches .NET 10 (`(short)1e10 == -1`).

`npm test` compares every export with the same DLL on the CLR: empty, negative and huge lengths, out-of-range indexes (traps vs. IndexOutOfRangeException and OverflowException), wraparound values, NaN/∞ conversions, and 300 seeded random values. It also checks the JS host boundary directly: `ShortParameter(40000)` is -25536, which the CLR cannot express. `CompoundElement.Increment` (`counts[i] += 1`) must be rejected at its `ldelema` with the method and IL offset.

Element types are classified by CLI identity: a primitive must come from a platform BCL assembly (`src/AssemblyReader/PlatformAssemblyIdentity.cs`), not just carry its name. `identity/` keeps Codex's two-DLL reproduction from the #75 review, where a third-party class named `System.Int16` reached through an extern alias must be rejected rather than lowered as `short[]`.

Limitations: `ldelema` (element references, so `a[i] += x` and `ref` element arguments), constant array initializers (`ldtoken` + `RuntimeHelpers.InitializeArray`), `ldelem`/`stelem` with a type token, and arrays of references or multi-dimensional arrays are not lowered yet.
