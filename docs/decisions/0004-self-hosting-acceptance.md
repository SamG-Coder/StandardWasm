# 0004 · Evidence required to mark self-hosting complete

Status: proposed (#78). It needs Codex's APPROVE because Codex owns the portable compiler chain.

GOAL.md defines self-hosting as: a generated compiler reads its own DLL, emits a second compiler, and that compiler reproduces the module and passes the library suite. The repository has two compilers:

- **Host compiler** (`Compiler/`): .NET 10 with Mono.Cecil and Binaryen. It compiles the CLR-equivalence stages and does not run in Wasm.
- **Portable compiler** (`src/AssemblyReader`, `src/PortableCompiler`): netstandard2.0 code compiled to Wasm. This is the compiler that must host itself.

This record pins what "reproduces the module" and "passes the library suite" mean, so that completion is a checkable claim.

## Producers

- **B**: the bootstrap, i.e. the portable compiler DLL compiled to Wasm once by the host compiler. B's own bytes are outside every comparison below.
- **G1 = B(DLL)**: the compiler module B emits when run on the portable compiler's own DLL with the compiler entry point selected.
- **G2 = G1(DLL)** and **G3 = G2(DLL)**: the same input and export selection, run through the previous generation.
- **C**: the CLR running the same portable compiler on the same DLL with the same export selection.

## Required evidence

1. **Fixed point that agrees with the CLR.** C, G1, G2 and G3 are byte-identical, and `npm test` asserts this in Node and Chromium. G1 = G2 alone is not enough, because a self-consistent miscompilation would pass it. G2 is the "second compiler" in GOAL.md, and G3 = G2 is the reproduction it must achieve.
2. **Harness boundary after B.** Once B has emitted G1, the JavaScript harness may only pass input bytes through a generated module's own transport exports, instantiate and validate the modules it returns, call their exports, and compare results. It may not compile IL, run any part of the compiler, or reuse B's exports, the host compiler, or Binaryen. Every compilation from G1 onward runs inside a generated module (#25, #26).
3. **The library suite is the CLR-equivalence suite.** G2 compiles the fixture DLL of every stage whose cases compare against the CLR, and the resulting modules pass the same oracle cases in Node and Chromium. Stages that only exercise the reader or the compiler itself also count, through their existing generated-code cases.
4. **Gaps are explicit.** If G2 cannot compile some stage fixture, its unsupported cases are listed in GOAL.md with a tracking issue. They are never routed to the host compiler. Self-hosting is not marked complete while any CLR-equivalence case is on that list.
5. **Reproducible record.** The PR that closes the milestone lists the exact commands, the .NET SDK, Node and browser versions, and the byte counts and SHA-256 hashes of C, G1, G2 and G3.

## Consequences

Each host compiler capability (64-bit integers, floating point, `switch`, `Math` intrinsics, exact float remainder, primitive arrays, and later additions) needs a portable counterpart before the milestone closes. Porting work can proceed per capability, each step with its own stage cases run through gen2. The host compiler stays available for comparison and for capabilities not yet ported.

## Current status (2026-11-26)

- Item 1: met on main since #24. `npm test` asserts C = G1 = G2 = G3 in Node and Edge: 128,404 bytes, SHA-256 cd9c80c7ccefbad75775d1b8d90e1fb8a8d4c86aba9522f42ea6d93981fc0e36.
- Item 2: open. Stage 12 still uses B's GC transport, which #25 / PR #26 replaces.
- Items 3–4: open. Codex's audit (#85) lists the concrete gaps (#82, #83, #84). Portable ports of i64, switch, diagnostics and floats are in review (#51, #55, #56, #61).
