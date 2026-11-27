# Stage 348 · Scalar bit reinterpretation

The compiler recognizes the authenticated CoreLib `Unsafe.BitCast<TFrom,TTo>` method for closed, equal-width integer and floating-point pairs. Those operands satisfy the managed helper's size and value-type checks. Wasm reinterpretation preserves bits directly, including signed zero and NaN payloads. The original managed path handles other type pairs.

```sh
npm run demo -- stage-348-scalar-bitcasts --no-rebuild
```

Use the generated compiler or set `STANDARDWASM_COMPILER_PATH`. The .NET 10 fixture compares both directions at 32 and 64 bits on Windows x86 CLR, Node and Edge. It checks finite values, signed-zero patterns and round-tripped quiet NaNs. Results and artifact hashes are written to `artifacts/stage-348-scalar-bitcasts/`.
