# Stage 55 · Allocation and delegate exceptions

Run `npm run demo -- stage-55-allocation-delegates`. Calling
`AllocationCleanup(-1)` returns `-17`. The negative length raises
`OverflowException` in the callee, then the caller runs cleanup before
entering its catch.

The portable compiler also raises `NullReferenceException` when invoking
a null supported scalar or event delegate. The fixtures check that event
arguments execute before the null check, that exceptions during argument
evaluation reach cleanup, and that null-conditional invocation still skips
arguments for a null callback.

Run `npm test` for CLR, Node and Edge comparisons and generated-compiler
replay. Allocation failures for nonnegative sizes still use the engine's
allocation behavior.
