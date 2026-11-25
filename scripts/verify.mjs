export async function verify(load, cases) {
    const modules = {};
    for (const name of new Set(cases.map(c => c.module))) {
        const module = await WebAssembly.compile(await load(name));
        if (WebAssembly.Module.imports(module).length) throw Error(name + " has runtime imports");
        modules[name] = (await WebAssembly.instantiate(module)).exports;
    }
    for (const c of cases) {
        let actual;
        try { actual = modules[c.module][c.method](...c.inputs); }
        catch (error) {
            if (!(error instanceof WebAssembly.RuntimeError)) throw error;
            actual = null;
        }
        if (actual !== c.expected) throw Error(JSON.stringify({ ...c, actual }));
    }
    if (modules.Sample.Countdown(1000000, 7) !== 1000007) throw Error("Deep tail call failed");
    return cases.length + " CLR comparisons and 1,000,000 tail calls passed";
}
