# Stage 88 · Payload memory-pressure experiment

    npm ci
    npm run demo -- stage-88-payload-pressure

This is an experiment for [decision 0025](../../docs/decisions/0025-pinnable-memory-model.md)'s open item on memory pressure (#196).

`pressure.wat` allocates 64 KiB linear-memory payloads owned by Wasm GC wrappers. Payloads are freed through `FinalizationRegistry` and reused. `pressure-core.mjs` churns them under five strategies and two allocation patterns, in Node (with `--experimental-wasm-jspi`) and Edge, with no forced GC:
- the baseline
- a GC "ballast" array on each wrapper (1/8 or 1/32 of the payload size)
- a 32 MiB budget whose allocation slow path yields one task through JSPI
- ballast and budget combined

The two patterns are 200 yielding rounds and one synchronous call. Each line reports peak linear memory, time, payloads freed, and how often the slow path yielded and found the budget still exceeded. For model C, the runner stops a configuration that has run past 60 seconds; it checks this between rounds, so it can't interrupt a single blocked call. Each run reports completed allocations separately from the requested churn. One ceiling case per model must stop with an explicit exhaustion. The stage fails if any check fails: a mismatched final live-list checksum, a missing or unexpected exhaustion, or an incomplete churn. A non-ceiling run stopped by the 60-second cap counts as an incomplete benchmark and also fails. The checks live in `check-results.mjs`, and the runner self-tests them on synthetic rows, including a timed-out partial row, before judging the real results.

`pressure-b.wat` runs the same churn under model B (all objects in linear memory, with a precise mark-sweep in the allocation slow path) and traces a 10,000-node live list on every collection.

Expected: no model C strategy holds the 32 MiB budget, while model B with collection holds about 33 MiB with sub-millisecond pauses. Ballast helps Edge for yielding code. Only the budgeted yield lowers synchronous peaks. Results go to `artifacts/payload-pressure/results.json`. The runner exits on its own and is not part of `npm test`.
