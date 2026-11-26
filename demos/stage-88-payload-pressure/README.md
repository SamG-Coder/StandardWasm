# Stage 88 · Payload memory-pressure experiment

    npm ci
    npm run demo -- stage-88-payload-pressure

This is an experiment for [decision 0025](../../docs/decisions/0025-pinnable-memory-model.md)'s open item on memory pressure (#196).

`pressure.wat` allocates 64 KiB linear-memory payloads owned by Wasm GC wrappers. Payloads are freed through `FinalizationRegistry` and reused. `pressure-core.mjs` churns them under five strategies and two allocation patterns, in Node (with `--experimental-wasm-jspi`) and Edge, with no forced GC:
- the baseline
- a GC "ballast" array on each wrapper (1/8 or 1/32 of the payload size)
- a 32 MiB budget whose allocation slow path yields one task through JSPI
- ballast and budget combined

The two patterns are 200 yielding rounds and one synchronous call. Each line reports peak linear memory, time, payloads freed, and how often the slow path yielded and found the budget still exceeded. Each configuration is capped at 60 seconds.

Expected: no strategy holds the 32 MiB budget. Ballast helps Edge for yielding code. Only the budgeted yield lowers synchronous peaks. Results go to `artifacts/payload-pressure/results.json`. The runner exits on its own and is not part of `npm test`.
