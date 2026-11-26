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
    x.configure(strategy.shift, strategy.budget);
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
        strategy: strategy.name, pattern: pattern.name, allocatedMiB: pattern.rounds * pattern.perRound / 16,
        peakMiB: x.pages() / 16, freed, ms, relieveCalls: x.relieveCalls(), relieveFailures: x.relieveFailures(),
        exhausted, timedOut
    };
}

export async function measurePressure(bytes) {
    const results = [];
    for (const strategy of strategies)
        for (const pattern of patterns)
            results.push(await runOne(bytes, strategy, pattern));
    return results;
}
