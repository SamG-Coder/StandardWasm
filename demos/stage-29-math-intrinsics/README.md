# Stage 29 · Math intrinsics

Run `npm run demo -- stage-29-math-intrinsics` from the root after `npm ci`. Expected output is `Hypotenuse(3, 4) = 5`.

Measures.cs is ordinary numeric code that calls `System.Math`: a hypotenuse, a quadratic root, banker's rounding to cents, grid cells with `Floor`, clamps built from `Max` and `Min`, and integer absolute differences. Each mapped call lowers to a Wasm instruction under [decision 0003](../../docs/decisions/0003-bcl-intrinsics.md), so the module still has no imports. `Math.Abs(int.MinValue)` traps where the CLR throws OverflowException. `UnmappedMath.Power` calls `Math.Pow`, which has no exact Wasm instruction, and must fail with `Unlinked call` naming the method and IL offset.

`npm test` compares every export with the same DLL on the CLR using `Object.is`. Inputs cover ±0, ±∞, NaN, halfway values (0.5, 1.5, 2.5, -0.5, 2^52 - 0.5), subnormals, int/long edges and 300 seeded random bit patterns.

Limitations: only the table in decision 0003 is mapped. Transcendental functions, `Math.Round` with digits or MidpointRounding, `Math.Sign` and `MathF` are not.
