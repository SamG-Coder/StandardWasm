# Stage 77 · Pointer memory-model experiment

    npm ci
    npm run demo -- stage-77-pointer-memory-model

This is an experiment, not a compiler stage. It backs [decision 0025](../../docs/decisions/0025-pinnable-memory-model.md).

`PointerPatterns.dll` (netstandard2.1, `Patterns.cs`) holds eight pointer patterns that upstream WinForms relies on:
- `fixed` over byte arrays and strings
- aliasing between managed and pointer access inside one `fixed` scope
- a pointer passed to a callee
- `Span<T>` slices
- `stackalloc`
- the address of a struct local
- the address of a struct array element

`Oracle/` runs them on the CLR and writes 34 expected results. `prototypes.mjs` lowers the same scenarios by hand under three candidate memory models:
- **A:** GC arrays plus copy-in/copy-out `fixed`.
- **B:** all objects in linear memory.
- **C:** GC wrappers over linear-memory payloads.

`measure-core.mjs` runs the scenarios and cost probes in Node and in Edge.

Two probes cover model C's lifetime and memory behavior:
- `liveness.wat` keeps a `Span` alive after its array variable is gone while freed payloads are reused, with and without the owner carried in the `Span`.
- `pressure.wat` churns 64 KiB arrays without forced collection.

Expected:
- A fails the 5 `FixedAliasing` cases. B and C pass all 34 in both engines.
- C's 20,000 tracked payloads are all reported dead after collection.
- The owner-carrying `Span` still reads 90; the ownerless one reads 17 (use after free).
- The pressure lines report peak linear memory: much higher in Edge than in Node, and the full allocation for a synchronous loop. Measurements go to `artifacts/pointer-memory-model/results.json`. The runner exits on its own and is not part of `npm test`.
