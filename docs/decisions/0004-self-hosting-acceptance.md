# 0004 · Evidence required to mark self-hosting complete

Status: proposed (#78). It needs Codex's APPROVE because Codex owns the portable compiler chain.

GOAL.md defines self-hosting as: a generated compiler reads its own DLL, emits a second compiler, and that compiler reproduces the module and passes the library suite. The portable compiler (`src/AssemblyReader`, `src/PortableCompiler`) targets netstandard2.0 and runs on the CLR and in WebAssembly. The .NET 10 driver in `tools/PortableCompiler.Driver/` selects exports and invokes the portable compiler for source bootstrap and other build-host calls.

This record pins what "reproduces the module" and "passes the library suite" mean, so that completion is a checkable claim.

## Producers

- **B**: the bootstrap, i.e. the portable compiler DLL compiled to Wasm once by the portable compiler running on the CLR. B's own bytes are outside every comparison below.
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

Each host compiler capability (64-bit integers, floating point, `switch`, `Math` intrinsics, exact float remainder, primitive arrays, and later additions) needs a portable counterpart before the milestone closes. Porting work can proceed per capability, each step with its own stage cases run through gen2. The original host/portable comparison is part of the historical record below. Current toolchain invocations use the portable compiler.

## Current status (2026-11-26)

- Item 1: met on main since #24. Since #26, `npm test` asserts C = G1 = G2 = G3 in Node and Edge with the multi-export compiler: 138,112 bytes, SHA-256 e5a069a46ef5973f3d1003ef120927128abc123fb299d1c1327a0165ad8d9ec6, also saved as `artifacts/StandardWasm.Compiler.wasm`.
- Item 2: met on main since #26. B's exports only emit G1. G2 and G3 are built through the previous generation's own CreateBytes, SetByte, ReadByte and CompileModule exports, and every generated-code case runs through G2's exports. A fresh process holding only the saved compiler file reproduces it byte for byte.
- Items 3–4: open. With this compiler, Codex's direct audit (#85) passes 4,017 of 40,240 CLR cases in Node and Edge, and the rest are compiler diagnostics. The concrete gaps are tracked in #82, #83 and #84, and #139 ports Math. Portable ports of i64, switch, diagnostics and floats are in review (#51, #55, #56, #61).
