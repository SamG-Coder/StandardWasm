# Stage 347 · Interior field reads in value-constructor arguments

The linker can keep a destination local pending while an argument loads a nested field through a temporary receiver. The interior address must be consumed by the immediately following field read. Control-flow entries and intervening calls retain their existing handling.

The .NET Standard fixture uses ordinary C# to construct two closed generic value types on their local addresses. It supplies byte and integer values from nested fields and checks the typed null-reference exception if evaluating the constructor argument fails.

```sh
npm run demo -- stage-347-interior-constructor-arguments --no-rebuild
```

Use the generated compiler or set `STANDARDWASM_COMPILER_PATH`. Windows x86 CLR, Node and Edge should return 231, 262157, and 619. The runner records hashes and results under `artifacts/stage-347-interior-constructor-arguments/`.
