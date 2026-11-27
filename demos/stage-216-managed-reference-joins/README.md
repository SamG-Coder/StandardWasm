# Stage 216 � Comparable managed references at stack joins

When two IL paths reach the same instruction with comparable CLR reference
types on the evaluation stack, the compiler keeps the wider type. The
emitter moves each surviving reference into the successor's typed spill
local while preserving object identity and nulls.

Run with Windows x86 .NET and Edge.

```powershell
npm run demo -- stage-216-managed-reference-joins
```

The ordinary .NET Standard fixture covers class-to-interface and subclass
assignability, null-coalescing with an interface and a concrete class, both
incoming path orders for a subclass and its base class, loops, and a switch.
Unrelated class references still produce a located diagnostic. Results are
written to `artifacts/stage-216-managed-reference-joins/results.json`.
