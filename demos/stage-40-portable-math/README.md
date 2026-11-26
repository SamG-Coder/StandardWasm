# Stage 40 · Portable Math intrinsics

Run from the repository root.

    npm run demo -- stage-40-portable-math

The Wasm-hosted compiler reads PortableMath.dll, compiles `Hypotenuse`, and the emitted module returns 5 for (3, 4).

This ports [decision 0003](../../docs/decisions/0003-bcl-intrinsics.md) to the self-hosted compiler. A `call` to a MemberRef is lowered in place only when all of the following hold (`src/AssemblyReader/PlatformMembers.cs`):
- its parent is a TypeRef `System.Math`,
- that TypeRef's resolution scope is an AssemblyRef whose name **and** 8-byte public key token belong to a platform BCL assembly (netstandard, mscorlib, System.Runtime, System.Runtime.Extensions, System.Private.CoreLib),
- its signature blob is exactly one of the mapped static overloads.

`src/PortableCompiler/MathWasm.cs` emits `f64.sqrt/abs/floor/ceil/trunc/nearest/min/max`, `f32.abs/min/max`, `select` with signed or unsigned compares for integer Max/Min, and a `MinValue` guard that throws the managed `OverflowException` tag for integer `Abs`. The tests compare uncaught overflow, catches in the same method, and catches across a method call.

Measures.cs uses every mapped overload in ordinary code (hypotenuse, banker's rounding to cents, clamps, integer absolute differences, grid cells). `npm test` compiles each method with the portable compiler on the CLR and checks the bytes and results in Node and Edge, both directly and through the self-recompiled compiler, against the CLR results. Inputs cover ±0, halfway values, subnormals, ±∞, NaN and integer edges. Two calls must stay diagnostics at their `call` IL offset: `Math.Sign` (outside the table) and `identity/`, a third-party `System.Math.Sqrt` reached through an extern alias (the CLR returns 11 for `Compute(9)`, and f64.sqrt would give 3).

Limitations: AssemblyRefs carrying a full public key rather than a token are not recognized (deriving the token needs SHA-1). Other Math members stay unsupported.
