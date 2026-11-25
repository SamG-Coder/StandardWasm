# 0004 · Evidence required to mark self-hosting complete

Status: proposed (#78). It needs Codex's APPROVE because Codex owns the portable compiler chain.

GOAL.md defines self-hosting as: a generated compiler reads its own DLL, emits a second compiler, and that compiler reproduces the module and passes the library suite. The repository has two compilers:

- **Host compiler** (`Compiler/`): .NET 10 with Mono.Cecil and Binaryen. It compiles the CLR-equivalence stages and does not run in Wasm.
- **Portable compiler** (`src/AssemblyReader`, `src/PortableCompiler`): netstandard2.0 code compiled to Wasm. This is the compiler that must host itself.

This record pins what "reproduces the module" and "passes the library suite" mean, so that completion is a checkable claim.

## Required evidence

1. **Fixed point that agrees with the CLR.** Run the portable compiler's entry point on its own DLL three ways: on the CLR (C), in the bootstrap Wasm module (gen1), and in the module gen1 emits (gen2). C, gen1 and gen2 must be byte-identical, and `npm test` must check this in Node and Chromium. gen1 = gen2 alone is not enough: a self-consistent miscompilation would pass it.
2. **No host compiler help.** After the bootstrap module emits gen1, all compilation and byte transport runs in emitted modules. The bootstrap module and the host compiler supply nothing beyond the input DLL bytes (#25).
3. **The library suite is the CLR-equivalence suite.** gen2 compiles the fixture DLL of every stage whose cases compare against the CLR, and the resulting modules pass the same oracle cases in Node and Chromium. Stages that only exercise the reader or the compiler itself also count, through their existing generated-code cases.
4. **Gaps are explicit.** If gen2 cannot compile some stage, the stage is listed in GOAL.md with a tracking issue. Self-hosting is not marked complete while any CLR-equivalence stage is on that list. No silent fallback to the host compiler is allowed.
5. **Reproducible record.** The PR that closes the milestone lists the exact commands, the .NET SDK, Node and browser versions, and the byte counts and hashes of C, gen1 and gen2.

## Consequences

Each host compiler capability (64-bit integers, floating point, `switch`, `Math` intrinsics, exact float remainder, primitive arrays, and later additions) needs a portable counterpart before the milestone closes. Porting work can proceed per capability, each step with its own stage cases run through gen2. The host compiler stays available for comparison and for capabilities not yet ported.

## Current status (2026-11-26)

- Item 1: reached on PR #24's head. C = gen1 = gen2 = gen3 = 128,438 bytes (SHA-256 prefix `d8f58f02af9170c5`), checked with a scratchpad probe. It is not yet a committed assertion.
- Item 2: open (#25 / PR #26).
- Items 3–4: open. Portable ports of i64, switch, diagnostics and floats are in review (#51, #55, #56, #61).
