# Stage 258 · Constrained Int32 object equality

A constrained `System.Int32` call to the actual `Object.Equals(object)` slot selects the original CoreLib override and passes the live managed Int32 location. The original override checks the boxed argument's runtime identity before reading the receiver. Its managed body uses the normal compiler path, including the existing primitive box test and unbox operations. Other constrained scalar receivers still require their own lowering.

Run `npm run demo -- stage-258-constrained-int32-equals`. Set `STANDARDWASM_COMPILER_PATH` to verify a frozen compiler. The same assembly runs in Windows x86 CLR, Node and Edge, covering signed matches, other box identities, a live field alias, null managed locations and both direct and constrained calls. The runner checks the located unsigned receiver diagnostic and writes evidence to `artifacts/stage-258-constrained-int32-equals/results.json`.
