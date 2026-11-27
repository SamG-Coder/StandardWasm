# Stage 289 · Closed type guards and Boolean branches

This .NET Standard fixture compares Windows x86 CLR, Node and Edge behavior when a closed `typeof(T)` predicate determines a compound Boolean branch. The first three cases contain a cast that would fail inside the impossible branch. The compiler proves the branch outcome while keeping the right operand evaluated, including a normal call that changes state. Reference-type decisions, control flow joining inside the right operand and `try/finally` retain their original evaluation.

```text
npm run demo -- stage-289-closed-type-boolean-guards
```

The expected results are 101, 103, 107, 109, 113 and 127. Set `STANDARDWASM_MONO_RUNTIME` to the matching runtime pack's `runtimes/browser-wasm` directory to check the same assembly with Mono metadata.

The proof covers an authenticated closed type predicate, an optional equality with zero or one, and at most one adjacent Int32 input to the absorbing operation. An interior branch target or exception region keeps the original records intact. Casts on reachable paths keep their ordinary interface rules.
