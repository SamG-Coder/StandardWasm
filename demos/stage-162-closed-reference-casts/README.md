# Stage 162 · Closed reference casts

Run `npm run demo -- stage-162-closed-reference-casts --no-rebuild` after
building the self-hosted compiler with `npm test`. Set `STANDARDWASM_COMPILER_PATH`
to verify another compiler. The runner compares the same compiled .NET Standard
2.0 assembly on Windows x86 CLR, Node, and Edge and records hashes and results
under `artifacts/stage-162-closed-reference-casts/`.

The fixture exercises `castclass` and `isinst` on constructed classes and
interfaces. It verifies null behavior and the typed `InvalidCastException`
for a different closed argument. It also substitutes the receiver's type
argument and a generic method argument when those occur in the cast metadata.
The generic class constructor initializes its own public and private readonly
instance fields through closed field references.

Expected outcomes are `101`, `103`, `107`, `109`, `113`, `127`, and `131`.
