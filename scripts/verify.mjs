import { verifyBootstrap } from "./bootstrap.mjs";
import { invoke, invokeGenerated } from "./invoke.mjs";

export async function verify(load, cases, saveCompiler) {
    const modules = {};
    for (const name of new Set(cases.map(c => c.module))) {
        const module = await WebAssembly.compile(await load(name));
        if (WebAssembly.Module.imports(module).length) throw Error(name + " has runtime imports");
        modules[name] = (await WebAssembly.instantiate(module)).exports;
    }
    const bootstrap = cases.find(c => c.bootstrap);
    for (const c of cases) {
        if (c.bootstrap) continue;
        let actual;
        try {
            actual = c.generated
                ? await invokeGenerated(modules[c.module], c.inputs, c.generated)
                : invoke(modules[c.module], c.method, c.inputs);
        }
        catch (error) {
            if (c.generated || !(error instanceof WebAssembly.RuntimeError)) throw error;
            actual = null;
        }
        if (actual !== c.expected) throw Error(JSON.stringify({ ...c, inputs: c.inputs.map(input => input?.bytes ? "<" + input.bytes.length + " bytes>" : input), actual }));
    }
    if (modules.Sample.Countdown(1000000, 7) !== 1000007) throw Error("Deep tail call failed");
    const summary = (cases.length - (bootstrap ? 1 : 0)) + " CLR comparisons and 1,000,000 tail calls passed";
    return bootstrap ? summary + "\n  Self-recompiled: " + await verifyBootstrap(modules[bootstrap.module], bootstrap, cases, saveCompiler) : summary;
}
