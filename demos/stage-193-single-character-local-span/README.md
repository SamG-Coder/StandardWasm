# Stage 193 · Single-character local span

`ReadOnlySpan<char>(ref readonly char)` over a local uses that local's packed character cell as its owner. The cell is allocated once per invocation and shared by the original local instructions and the span. A direct indirect store through the local can use the same path when its value and store are contiguous and there is no alternate control-flow entry.

Run `npm run demo -- stage-193-single-character-local-span`. Supply a prebuilt compiler with `STANDARDWASM_COMPILER_PATH` and `--no-rebuild` to pin verification.

The same C# assembly checks direct and indirect writes, two spans over one local, equal characters in different locals, distinct owners in separate invocations, and an empty view anchored at the end. The span can be passed by value to a managed reader. This lowering applies to the exact platform constructor over a local in a method with no outward managed location or byref-like return. General character references, including the null-reference constructor case, require their own location and lifetime binding.
