export async function verify(load, cases) {
    const modules = {};
    for (const name of new Set(cases.map(c => c.module))) {
        const module = await WebAssembly.compile(await load(name));
        if (WebAssembly.Module.imports(module).length) throw Error(name + " has runtime imports");
        modules[name] = (await WebAssembly.instantiate(module)).exports;
    }
    // CLR exception type -> engine trap message -> count. Wasm has no typed exceptions yet, so a CLR
    // ArithmeticException (expected === null) must surface as a RuntimeError trap. The pairing is
    // reported, not asserted, because trap messages are engine-specific.
    const traps = new Map();
    for (const c of cases) {
        let actual;
        try { actual = modules[c.module][c.method](...c.inputs); }
        catch (error) {
            if (!(error instanceof WebAssembly.RuntimeError)) throw error;
            actual = null;
            const key = (c.exception ?? "no CLR exception") + " -> trap '" + error.message + "'";
            traps.set(key, (traps.get(key) ?? 0) + 1);
        }
        if (actual !== c.expected) throw Error(JSON.stringify({ ...c, actual }));
    }
    if (modules.Sample.Countdown(1000000, 7) !== 1000007) throw Error("Deep tail call failed");
    const trapSummary = [...traps].sort().map(([key, count]) => "\n  " + count + " x " + key).join("");
    return cases.length + " CLR comparisons and 1,000,000 tail calls passed" + trapSummary;
}
