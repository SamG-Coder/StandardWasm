// Runs in Node (with --experimental-wasm-jspi) and Edge. No gc() is ever called: collection follows the engine.
const strategies = [
    { name: "baseline", shift: 0, budget: 0 },
    { name: "ballast 1/8", shift: 3, budget: 0 },
    { name: "ballast 1/32", shift: 5, budget: 0 },
    { name: "budget 32 MiB", shift: 0, budget: 32 << 20 },
    { name: "ballast 1/32 + budget 32 MiB", shift: 5, budget: 32 << 20 }
];
const patterns = [
    { name: "200 yielding rounds of 50", rounds: 200, perRound: 50 },
    { name: "one synchronous call of 5000", rounds: 1, perRound: 5000 }
];
const liveRegistries = [];

async function runOne(bytes, strategy, pattern) {
    let x, freed = 0;
    const registry = new FinalizationRegistry(p => { freed++; x.free(p); });
    liveRegistries.push(registry);
    // relieve: yield one task turn so pending finalization callbacks can run. MessageChannel avoids the 4 ms clamp
    // that repeated setTimeout calls get, which made a per-allocation yield take minutes in the first version.
    const channel = new MessageChannel();
    const yieldTurn = () => new Promise(resolve => { channel.port1.onmessage = resolve; channel.port2.postMessage(0); });
    const relieve = new WebAssembly.Suspending(async () => {
        await yieldTurn();
        return x.live() <= strategy.budget ? 1 : 0;
    });
    ({ instance: { exports: x } } = await WebAssembly.instantiate(bytes,
        { host: { track: (owner, p) => registry.register(owner, p), relieve } }));
    x.configure(strategy.shift, strategy.budget, strategy.limitPages ?? 0);
    const churn = WebAssembly.promising(x.churn);
    const start = performance.now();
    let exhausted = false;
    let timedOut = false;
    for (let round = 0; round < pattern.rounds && !exhausted; round++) {
        exhausted = (await churn(pattern.perRound)) === -1;
        if (pattern.rounds > 1) await new Promise(resolve => setTimeout(resolve, 0));
        if (performance.now() - start > 60000) { timedOut = true; break; } // cap each configuration at one minute
    }
    const ms = +(performance.now() - start).toFixed(1);
    channel.port1.close();
    liveRegistries.splice(liveRegistries.indexOf(registry), 1);
    return {
        strategy: strategy.name, pattern: pattern.name, requestedMiB: pattern.rounds * pattern.perRound / 16,
        completedMiB: x.completed() / 16, completed: x.completed(),
        peakMiB: x.pages() / 16, freed, ms, relieveCalls: x.relieveCalls(), relieveFailures: x.relieveFailures(),
        exhausted, timedOut
    };
}

export async function measurePressure(bytes) {
    const results = [];
    for (const strategy of strategies)
        for (const pattern of patterns)
            results.push(await runOne(bytes, strategy, pattern));
    // Exhaustion: a 4 MiB ceiling (64 pages) must stop the synchronous churn with an explicit -1,
    // after fewer allocations than requested.
    results.push(await runOne(bytes, { name: "baseline, 4 MiB ceiling", shift: 0, budget: 0, limitPages: 64 }, patterns[1]));
    return results;
}

// Model B: all objects in linear memory with a synchronous mark-sweep in the allocation slow path (pressure-b.wat).
// A 10,000-node list stays live and is traced by every collection; its sum must survive (49,995,000).
export async function measureModelB(bytes) {
    const results = [];
    const configurations = [[0, 10000, 0], [32 << 20, 10000, 0], [32 << 20, 1000000, 0]];
    for (const [budget, liveNodes, limitPages] of configurations)
        for (const pattern of patterns) {
            const { instance: { exports: x } } = await WebAssembly.instantiate(bytes,
                { host: { now: () => performance.now() } });
            x.configure(budget, limitPages);
            if (x.buildLive(liveNodes) !== 0) throw Error("buildLive exhausted memory");
            const start = performance.now();
            let exhausted = false;
            for (let round = 0; round < pattern.rounds && !exhausted; round++) {
                exhausted = x.churn(pattern.perRound) === -1;
                if (pattern.rounds > 1) await new Promise(resolve => setTimeout(resolve, 0));
            }
            results.push({
                strategy: (budget ? `B: collect every ${budget >> 20} MiB` : "B: no collection") + `, ${liveNodes / 1000}k live`,
                pattern: pattern.name, requestedMiB: pattern.rounds * pattern.perRound / 16,
                completedMiB: x.completed() / 16, completed: x.completed(), peakMiB: x.pages() / 16,
                ms: +(performance.now() - start).toFixed(1), collections: x.collections(),
                maxPauseMs: +(x.maxPauseUs() / 1000).toFixed(2), totalPauseMs: +(x.totalPauseUs() / 1000).toFixed(1),
                liveIntact: x.liveSum() === ((liveNodes * (liveNodes - 1) / 2) | 0), exhausted
            });
        }
    // Exhaustion: without collection and with a 200-page (12.5 MiB) ceiling, churn must stop with an explicit -1
    // and the live list built before it must still be intact.
    {
        const { instance: { exports: x } } = await WebAssembly.instantiate(bytes, { host: { now: () => performance.now() } });
        x.configure(0, 200);
        if (x.buildLive(10000) !== 0) throw Error("buildLive exhausted memory below the ceiling");
        const exhausted = x.churn(5000) === -1;
        results.push({ strategy: "B: no collection, 10k live, 12.5 MiB ceiling", pattern: patterns[1].name,
            requestedMiB: 5000 / 16, completedMiB: x.completed() / 16, completed: x.completed(), peakMiB: x.pages() / 16,
            ms: 0, collections: x.collections(), maxPauseMs: 0, totalPauseMs: 0,
            liveIntact: x.liveSum() === 49995000, exhausted });
    }
    return results;
}

// Frame cost of model B's rooting (spill.wat): the same recursive tree sum as Wasm GC structs (C), as unrooted linear
// nodes (lower bound) and as linear nodes with every live reference spilled to a shadow-stack slot around calls (B).
export async function measureSpill(bytes) {
    const { instance: { exports: x } } = await WebAssembly.instantiate(bytes);
    x.build(18); // 262,143 nodes
    const expected = x.gcSum(1);
    const results = {};
    for (const name of ["gcSum", "linearSum", "spillSum"]) {
        if (x[name](1) !== expected) throw Error(name + " disagrees with gcSum");
        x[name](5); // warm-up
        const samples = [];
        for (let i = 0; i < 7; i++) {
            const start = performance.now();
            x[name](20);
            samples.push(performance.now() - start);
        }
        samples.sort((a, b) => a - b);
        results[name] = +samples[3].toFixed(2); // median ms for 20 traversals of 262,143 nodes
    }
    return results;
}
