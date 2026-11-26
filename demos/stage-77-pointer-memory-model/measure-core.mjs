// Runs in Node and in Edge. It checks every CLR oracle case against each candidate model, then times the cost
// probes and checks whether model C's linear-memory payloads can be reclaimed when their GC wrapper dies.
export async function measure(models, cases, gc) {
    const report = {};
    for (const [name, bytes] of Object.entries(models)) {
        const { instance } = await WebAssembly.instantiate(bytes);
        const x = instance.exports;
        const mismatches = [];
        for (const c of cases) {
            x.reset();
            let actual;
            try { actual = x[c.scenario](c.input); } catch (e) { actual = "trap: " + e.message; }
            if (!Object.is(actual, c.expected)) mismatches.push({ ...c, actual });
        }
        const time = (fn, repeat = 5) => {
            fn(); // warm-up: compile tiers and first-touch memory
            const samples = [];
            for (let i = 0; i < repeat; i++) {
                const start = performance.now();
                fn();
                samples.push(performance.now() - start);
            }
            samples.sort((a, b) => a - b);
            return +samples[samples.length >> 1].toFixed(3);
        };
        const model = {
            correct: cases.length - mismatches.length, cases: cases.length, mismatches,
            // median ms
            accessMs: time(() => x.benchAccess(1 << 20, 20)),         // 20.97M managed element reads
            fixedSmallMs: time(() => x.benchFixed(64, 20000)),        // 20k fixed scopes over a 64-byte array
            fixedLargeMs: time(() => x.benchFixed(65536, 2000)),      // 2k fixed scopes over a 64 KiB array
            allocMs: time(() => x.benchAlloc(200000, 32)),            // 200k new byte[32]
        };
        if (x.newTrackedBytes) model.reclaim = await reclaim(x, gc);
        report[name] = model;
    }
    return report;
}

const liveRegistries = [];

// Model C needs payload reclamation: when a wrapper dies, its linear block must be freed. The candidate mechanism
// is FinalizationRegistry on the wrapper. This measures its per-allocation cost and whether callbacks arrive.
async function reclaim(x, gc) {
    const count = 20000;
    let freed = 0;
    const registry = new FinalizationRegistry(() => { freed++; });
    x.reset();
    const start = performance.now();
    for (let i = 0; i < count; i++) registry.register(x.newTrackedBytes(32), i);
    const registerMs = +(performance.now() - start).toFixed(3);
    let rounds = 0;
    // A registry that becomes unreachable is itself collected and never calls back, so keep it alive while waiting.
    liveRegistries.push(registry);
    if (gc) {
        for (; rounds < 50 && freed < count; rounds++) {
            gc();
            await new Promise(resolve => setTimeout(resolve, 20));
        }
    }
    liveRegistries.splice(liveRegistries.indexOf(registry), 1);
    return { allocated: count, freed, registerMs, gcRounds: rounds, gcAvailable: !!gc };
}

// Liveness and reuse (Codex review on #171). A Span outlives its array variable while freed payloads are reused.
// keepOwner = 1 carries the wrapper in the Span (the adopted rule); keepOwner = 0 is the ownerless representation.
export async function liveness(bytes, gc) {
    const results = [];
    for (const keepOwner of [1, 0]) {
        let x, freed = 0;
        const registry = new FinalizationRegistry(p => { freed++; x.free(p); });
        liveRegistries.push(registry);
        ({ instance: { exports: x } } = await WebAssembly.instantiate(bytes,
            { host: { track: (owner, p) => registry.register(owner, p) } }));
        x.hold(0x5a, keepOwner);
        for (let round = 0; round < 30; round++) {
            gc?.();
            await new Promise(resolve => setTimeout(resolve, 10));
            x.churn(50, 0x11);
        }
        results.push({ keepOwner: !!keepOwner, spanByte: x.read(0), expected: 0x5a, freed, gcAvailable: !!gc });
        liveRegistries.splice(liveRegistries.indexOf(registry), 1);
    }
    return results;
}

// Memory pressure without forced collection: 64 KiB payloads are allocated and dropped, either in rounds that yield
// to the event loop or in one synchronous loop. Linear memory never shrinks, so its final size is the peak.
export async function pressure(bytes) {
    const results = [];
    for (const [rounds, perRound] of [[200, 50], [1, 5000]]) {
        let x, freed = 0;
        const registry = new FinalizationRegistry(p => { freed++; x.free(p); });
        liveRegistries.push(registry);
        ({ instance: { exports: x } } = await WebAssembly.instantiate(bytes,
            { host: { track: (owner, p) => registry.register(owner, p) } }));
        let exhausted = false;
        for (let round = 0; round < rounds && !exhausted; round++) {
            exhausted = x.churn(perRound) === -1;
            if (rounds > 1) await new Promise(resolve => setTimeout(resolve, 0));
        }
        const freedWhenDone = freed;
        await new Promise(resolve => setTimeout(resolve, 200));
        results.push({ pattern: rounds > 1 ? `${rounds} yielding rounds of ${perRound}` : `one synchronous loop of ${perRound}`,
            allocatedMiB: rounds * perRound / 16, peakMiB: x.pages() / 16, freedWhenDone, freedAfter200ms: freed,
            allocations: rounds * perRound, exhausted });
        liveRegistries.splice(liveRegistries.indexOf(registry), 1);
    }
    return results;
}
