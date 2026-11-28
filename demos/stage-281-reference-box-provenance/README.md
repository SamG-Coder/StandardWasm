# Stage 281 · Closed reference boxes at interface calls

A `box T` on a closed managed reference preserves the actual reference and null. When an immediately consuming call needs a proven implemented interface in its final slot, the compiler retains that interface verification type. The proof requires the call to have one incoming edge. Ordinary object widening and other forms of boxing use their existing paths.

Run `npm run demo -- stage-281-reference-box-provenance`. Set `STANDARDWASM_COMPILER_PATH` for a frozen compiler, and optionally set `STANDARDWASM_MONO_CORELIB` to verify with the browser Mono implementation. Windows x86 CLR, Node and Edge compare direct and virtual interface calls, exact identity, null, and a normal object join. A shared interface call keeps its located diagnostic.
