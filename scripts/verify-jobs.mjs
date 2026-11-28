// Independent pieces of verify() that can run concurrently, in Node worker threads or in
// separate browser contexts. Each job runs in a fresh engine instance. planJobs() and
// combineResults() keep the assertions and the failure diagnostics of the sequential suite.
// The suite still fails on any single mismatch.
import { decode, invoke, invokeGenerated, managedExceptionType } from "./invoke.mjs";

// Failure reports spell out values JSON would hide: BigInt, -0 (JSON prints 0) and non-finite Numbers.
export const printable = value => JSON.stringify(value, (key, v) =>
    typeof v === "bigint" ? v + "n" : Object.is(v, -0) ? "-0" : typeof v === "number" && !Number.isFinite(v) ? String(v) : v);

const hexBytes = hex => Uint8Array.from({ length: hex.length / 2 }, (_, i) => parseInt(hex.substr(2 * i, 2), 16));
export const toHex = bytes => Array.from(bytes, byte => byte.toString(16).padStart(2, "0")).join("");

// Direct comparisons stay grouped by module and keep their oracle order. Cases of one module
// share one instance, as they do on the CLR, so static state evolves exactly as before.
// Generated-code cases each compile and instantiate a fresh module, so they shard by case.
export function planJobs(cases, shards, generatedShards = shards) {
    const bootstrap = cases.find(c => c.bootstrap);
    const groups = new Map();
    const generated = [];
    cases.forEach((c, index) => {
        if (c.bootstrap) return;
        if (c.generated) { generated.push(index); return; }
        if (!groups.has(c.module)) groups.set(c.module, []);
        groups.get(c.module).push(index);
    });
    // Largest groups first onto the least loaded shard.
    const bins = Array.from({ length: shards }, () => ({ weight: 0, indices: [] }));
    for (const indices of [...groups.values()].sort((a, b) => b.length - a.length)) {
        const bin = bins.reduce((best, next) => next.weight < best.weight ? next : best);
        bin.weight += indices.length;
        bin.indices.push(...indices);
    }
    // Generated cases are much heavier than direct calls; spread them across their own shards.
    const generatedBins = Array.from({ length: generatedShards }, () => []);
    generated.forEach((index, n) => generatedBins[n % generatedShards].push(index));
    // Pools start jobs in array order, so the longest jobs (the generations) come first.
    // verifyConcurrently() reports failures by kind, independent of this order.
    const jobs = [];
    if (bootstrap) {
        // Decision 0004 generations. G2 = G1(DLL) and G3 = G2(DLL). G1, G2 and G3 must all
        // equal C (the CLR output), so the job for G(n+1) starts from C's bytes at once.
        // combineResults() accepts it only after G(n) is proven byte-identical to C, so the
        // instance is the very module G(n) returned (decision 0102).
        jobs.push({ kind: "generation", generation: 1 });
        jobs.push({ kind: "generation", generation: 2 });
        jobs.push({ kind: "generation", generation: 3 });
        // Every generated-code case runs through G2's exports.
        for (const indices of generatedBins) if (indices.length) jobs.push({ kind: "second", indices });
    }
    for (const indices of generatedBins) if (indices.length) jobs.push({ kind: "compare", indices });
    for (const bin of bins) if (bin.indices.length) jobs.push({ kind: "compare", indices: bin.indices.sort((a, b) => a - b) });
    return jobs;
}

async function instantiateAll(load, names) {
    const modules = {};
    for (const name of names) {
        const module = await WebAssembly.compile(await load(name));
        if (WebAssembly.Module.imports(module).length) throw Error(name + " has runtime imports");
        modules[name] = (await WebAssembly.instantiate(module)).exports;
    }
    return modules;
}

async function compareCases(load, cases, indices) {
    const modules = await instantiateAll(load, new Set(indices.map(i => cases[i].module)));
    // Managed exceptions carry an exact type. Keep legacy engine traps
    // visible for fixtures that still expect an exception on the CLR.
    const traps = {};
    let managed = 0;
    for (const index of indices) {
        const c = cases[index];
        let actual;
        try {
            actual = c.generated
                ? await invokeGenerated(modules[c.module], c.inputs, c.generated)
                : invoke(modules[c.module], c.method, c.inputs);
        }
        catch (error) {
            if (c.generated || c.expected !== null || !c.exception) throw error;
            const type = managedExceptionType(modules[c.module], error);
            if (type === c.exception) {
                managed++;
            } else if (error instanceof WebAssembly.RuntimeError) {
                const key = c.exception + " -> trap '" + error.message + "'";
                traps[key] = (traps[key] ?? 0) + 1;
            } else {
                throw error;
            }
            actual = null;
        }
        // SameValue (Object.is) treats NaN as equal to NaN and +0 as different from -0, so floating-point results
        // compare bit-exactly up to NaN payload. It also rejects a Number where the CLR produced a 64-bit BigInt.
        if (!Object.is(actual, decode(c.expected)))
            throw Error(printable({ ...c, inputs: c.inputs.map(input => input?.bytes ? "<" + input.bytes.length + " bytes>" : input), actual }));
    }
    // The deep tail calls run on the instance that served the module's cases.
    if (modules.Sample && modules.Sample.Countdown(1000000, 7) !== 1000007) throw Error("Deep tail call failed");
    if (modules.LongIntegers && modules.LongIntegers.Countdown(1000000n, 0n) !== 3000000n)
        throw Error("Deep i64 tail call failed");
    return { kind: "compare", count: indices.length, managed, traps, tailCalls: !!modules.Sample, wideTailCalls: !!modules.LongIntegers };
}

// One self-compilation through a compiler's own transport exports (decision 0004 item 2).
export async function buildGeneration(compiler, fixture) {
    function upload(values) {
        const array = compiler.CreateBytes(values.length);
        for (let i = 0; i < values.length; i++) compiler.SetByte(array, i, values[i]);
        return array;
    }
    const capacity = fixture.capacity ?? (1 << 20);
    const output = compiler.CreateBytes(capacity);
    const length = compiler.CompileModule(upload(fixture.inputs[0].bytes), upload(fixture.inputs[1].bytes), output);
    if (length <= 0 || length > capacity)
        throw Error("Self-compilation returned " + length);
    const bytes = new Uint8Array(length);
    for (let i = 0; i < length; i++) bytes[i] = compiler.ReadByte(output, i);
    return bytes;
}

// Instantiates a generated compiler and checks its import-free shape and exports.
export async function instantiateCompiler(bytes, fixture) {
    const module = await WebAssembly.compile(bytes);
    if (WebAssembly.Module.imports(module).length) throw Error("Compiler has runtime imports");
    const { exports } = await WebAssembly.instantiate(module);
    for (const name of fixture.exports)
        if (typeof exports[name] !== "function") throw Error("Compiler lacks export " + name);
    return exports;
}

export async function runJob(job, load, cases) {
    if (job.kind === "compare") return compareCases(load, cases, job.indices);
    const fixture = cases.find(c => c.bootstrap);
    if (job.kind === "generation") {
        // G1 is B's output; B is the CLR-hosted bootstrap module, outside every comparison.
        const compiler = job.generation === 1
            ? await instantiateAll(load, [fixture.module]).then(modules => modules[fixture.module])
            : await instantiateCompiler(hexBytes(fixture.expectedHex), fixture);
        const bytes = await buildGeneration(compiler, fixture);
        // The produced compiler must itself instantiate with every export (decision 0004).
        await instantiateCompiler(bytes, fixture);
        // Compare here, so only a flag crosses a worker or DevTools boundary, not megabytes of hex.
        return { kind: "generation", generation: job.generation, matchesClr: toHex(bytes) === fixture.expectedHex, length: bytes.length };
    }
    if (job.kind === "second") {
        const second = await instantiateCompiler(hexBytes(fixture.expectedHex), fixture);
        for (const index of job.indices) {
            const c = cases[index];
            const actual = await invokeGenerated(second, c.inputs, c.generated);
            if (!Object.is(actual, decode(c.expected)))
                throw Error("Self-recompiled compiler failed " + c.module + " " + c.fixture);
        }
        return { kind: "second", tested: job.indices.length };
    }
    throw Error("Unknown verification job " + job.kind);
}

// Runs every job through `execute` (which may run them concurrently) and reports the outcome
// in the sequential suite's order: CLR comparisons, then G1 = C, then G2 = G3 = G1, then the
// generated-code cases through G2. The first failure in that order is the one rethrown.
export async function verifyConcurrently(cases, jobs, execute, saveCompiler) {
    const settled = await Promise.allSettled(jobs.map(execute));
    const failure = kinds => {
        const index = jobs.findIndex((job, i) => kinds(job) && settled[i].status === "rejected");
        if (index >= 0) throw settled[index].reason;
    };
    failure(job => job.kind === "compare");
    const results = settled.map(s => s.value);
    const compared = results.filter(r => r.kind === "compare");
    if (!compared.some(r => r.tailCalls)) throw Error("Deep tail call failed");
    let managed = 0;
    const traps = new Map();
    for (const r of compared) {
        managed += r.managed;
        for (const [key, count] of Object.entries(r.traps)) traps.set(key, (traps.get(key) ?? 0) + count);
    }
    const total = compared.reduce((sum, r) => sum + r.count, 0);
    const trapSummary = [...traps].sort().map(([key, count]) => "\n  " + count + " x " + key).join("");
    const summary = total + " CLR comparisons and 1,000,000 tail calls passed" +
        (managed ? `\n  ${managed} typed managed exceptions matched` : "") + trapSummary;
    const fixture = cases.find(c => c.bootstrap);
    if (!fixture) return summary;

    const generation = n => {
        const index = jobs.findIndex(job => job.kind === "generation" && job.generation === n);
        if (settled[index].status === "rejected") throw settled[index].reason;
        return results[index].matchesClr;
    };
    if (!generation(1)) throw Error("First generated compiler differs from the CLR output");
    // G2 ran on an instance of C's bytes, which G1 now equals; likewise G3 on G2's.
    for (const n of [2, 3])
        if (!generation(n)) throw Error("Self-recompiled compiler differs from the first generation");
    failure(job => job.kind === "second");
    const tested = results.filter(r => r.kind === "second").reduce((sum, r) => sum + r.tested, 0);
    // G2 equals C byte for byte, so C's bytes are G2's bytes.
    const second = hexBytes(fixture.expectedHex);
    if (saveCompiler) saveCompiler(second);
    return summary + "\n  Self-recompiled: " + second.length + " byte-identical compiler bytes and " + tested + " generated-code cases";
}
