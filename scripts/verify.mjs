import { decode, invoke, invokeGenerated } from "./invoke.mjs";
import { verifyBootstrap } from "./bootstrap.mjs";

// Failure reports spell out values JSON would hide: BigInt, -0 (JSON prints 0) and non-finite Numbers.
const printable = value => JSON.stringify(value, (key, v) =>
    typeof v === "bigint" ? v + "n" : Object.is(v, -0) ? "-0" : typeof v === "number" && !Number.isFinite(v) ? String(v) : v);

export async function verify(load, cases, saveCompiler) {
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
            const key = (c.exception ?? "no CLR exception") + " -> trap '" + error.message + "'";
            traps.set(key, (traps.get(key) ?? 0) + 1);
        }
        // SameValue (Object.is) treats NaN as equal to NaN and +0 as different from -0, so floating-point results
        // compare bit-exactly up to NaN payload. It also rejects a Number where the CLR produced a 64-bit BigInt.
        if (!Object.is(actual, decode(c.expected)))
            throw Error(printable({ ...c, inputs: c.inputs.map(input => input?.bytes ? "<" + input.bytes.length + " bytes>" : input), actual }));
    }
    if (modules.Sample.Countdown(1000000, 7) !== 1000007) throw Error("Deep tail call failed");
    if (modules.LongIntegers && modules.LongIntegers.Countdown(1000000n, 0n) !== 3000000n)
        throw Error("Deep i64 tail call failed");
    const trapSummary = [...traps].sort().map(([key, count]) => "\n  " + count + " x " + key).join("");
    const summary = (cases.length - (bootstrap ? 1 : 0)) +
        " CLR comparisons and 1,000,000 tail calls passed" + trapSummary;
    return bootstrap ? summary + "\n  Self-recompiled: " +
        await verifyBootstrap(modules[bootstrap.module], bootstrap, cases, saveCompiler) : summary;
}
