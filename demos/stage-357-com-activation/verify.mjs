import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";
import { root } from "../../scripts/toolchain.mjs";
import { packEntry } from "../stage-74-executable-entry/companions.mjs";
import { serveLaunch } from "../stage-74-executable-entry/launch-server.mjs";
import { instantiateManagedModule } from "../stage-74-executable-entry/resource-host.mjs";

const stage = join(root, "demos/stage-357-com-activation");
const evidence = join(root, "artifacts/stage-357-com-activation");
mkdirSync(evidence, { recursive: true });
const oraclePath = join(evidence, "oracle.json");
runOn32BitClr(wasm32Runtime(), join(stage, "Oracle"), join(stage, "Oracle/bin/Release/net10.0/Oracle.dll"), [oraclePath]);
const { clr, assembly, dependencies, rows, ids, cases } = JSON.parse(readFileSync(oraclePath, "utf8"));
const [owner, ...companions] = [assembly, ...dependencies].map(path => ({ bytes: readFileSync(path) }));
const [pack] = packEntry(owner, companions, rows.activate);
const compilerPath = process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts/StandardWasm.Compiler.wasm");
const compiler = readFileSync(compilerPath);

async function exercise(compiler, pack, rows, ids, instantiate) {
    const { instance: { exports: tool } } = await WebAssembly.instantiate(compiler);
    function upload(bytes) {
        const data = tool.CreateBytes(bytes.length);
        for (let i = 0; i < bytes.length; i++) tool.SetByte(data, i, bytes[i]);
        return data;
    }

    const bundle = upload(new Uint8Array(pack)), compiled = tool.CreateBytes(2 << 20), diagnostic = tool.CreateDiagnostic();
    function compile(names) {
        const manifest = [];
        for (const name of names) {
            const encoded = new TextEncoder().encode(name), entry = new Uint8Array(encoded.length + 8);
            const header = new DataView(entry.buffer);
            header.setUint32(0, rows[name], true);
            header.setUint32(4, encoded.length, true);
            entry.set(encoded, 8);
            manifest.push(...entry);
        }
        const status = tool.CompileBundleDetailed(bundle, upload(manifest), compiled, diagnostic);
        return { status, location: [0, 1, 2].map(index => tool.ReadDiagnostic(diagnostic, index)) };
    }

    const linked = compile(["activate", "lookup"]);
    if (linked.status <= 0) throw Error(`COM activation compile ${linked.status} at ${linked.location}`);
    const module = await WebAssembly.compile(Uint8Array.from({ length: linked.status }, (_, i) => tool.ReadByte(compiled, i)));
    const signature = "stdcall:i32(p32,u32,p32,p32)";
    const observed = {};
    let factoryCalls = 0, lookupCalls = 0;

    function writeGuid(view, address, guid) {
        const [first, second, third, ...rest] = guid.split("-");
        view.setUint32(address, Number.parseInt(first, 16), true);
        view.setUint16(address + 4, Number.parseInt(second, 16), true);
        view.setUint16(address + 6, Number.parseInt(third, 16), true);
        const tail = rest.join("");
        for (let i = 0; i < 8; i++) view.setUint8(address + 8 + i, Number.parseInt(tail.slice(i * 2, i * 2 + 2), 16));
    }

    async function launch(registered) {
        let memory;
        function configureCom(registry) {
            ({ memory } = registry);
            if (!registered) return;
            registry.registerClass(ids.tableClass.toUpperCase(), new Map([[ids.tableInterface, () => {
                factoryCalls++;
                return new Map([[5, { signature, invoke(receiver, cookie, iid, target) {
                    lookupCalls++;
                    const words = new DataView(memory.buffer);
                    if (!receiver || cookie !== 0xffffffff || words.getUint8(iid + 8) !== 0xc0)
                        throw Error("COM activation did not preserve the registered interface and its call arguments");
                    words.setUint32(target, 0, true);
                    return 0x80070057;
                } }]]);
            }]]));
        }
        const guest = await instantiate(module, tool, [], {}, null, null, null, { configureCom });
        const base = memory.grow(1) * 65536;
        const addresses = {};
        Object.entries(ids).forEach(([key, guid], index) => {
            addresses[key] = base + 16 * index;
            writeGuid(new DataView(memory.buffer), addresses[key], guid);
        });
        const output = base + 64;
        const words = () => new DataView(memory.buffer);
        function activate(classId, outer, context, iid, nullOutput = false) {
            words().setUint32(output, 17, true);
            const hr = guest.exports.activate(classId, outer, context, iid, nullOutput ? 0 : output);
            const result = words().getUint32(output, true);
            return { hr, activated: result !== 0 && result !== 17, cleared: result === 0, result };
        }
        return { guest, addresses, output, words, activate };
    }

    const { guest, addresses: { tableClass, tableInterface, absent, unknown }, output, words, activate } = await launch(true);
    const { result: receiver, ...activation } = activate(tableClass, 0, 1, tableInterface);
    words().setUint32(output, 17, true);
    const lookup = guest.exports.lookup(receiver, 0xffffffff, unknown, output);
    observed.success = { ...activation, lookup, lookupCleared: words().getUint32(output, true) === 0 };
    for (const [name, args] of Object.entries({
        unknownClass: [absent, 0, 1, tableInterface],
        unknownInterface: [tableClass, 0, 1, absent],
        wrongContext: [tableClass, 0, 4, tableInterface],
        aggregated: [tableClass, receiver, 1, unknown],
        nullOutput: [tableClass, 0, 1, tableInterface, true]
    })) {
        const { result, ...observation } = activate(...args);
        observed[name] = observation;
    }
    const isolated = await launch(false);
    const { result, ...unregistered } = isolated.activate(isolated.addresses.tableClass, 0, 1, isolated.addresses.tableInterface);
    const badPointer = activate(0, 0, 1, tableInterface);
    return { observed, unregistered, badPointer, factoryCalls, lookupCalls, imports: WebAssembly.Module.imports(module),
        rejectedOutput: compile(["wrongOutput"]), rejectedLibrary: compile(["wrongLibrary"]) };
}

const node = await exercise(compiler, pack, rows, ids, instantiateManagedModule);
assert.deepEqual(node.observed, cases);
assert.deepEqual(node.unregistered, cases.unknownClass);
assert.deepEqual(node.badPointer, { hr: 0x80004003 | 0, activated: false, cleared: true, result: 0 });
assert.equal(node.factoryCalls, 1);
assert.equal(node.lookupCalls, 1);
assert.deepEqual(node.rejectedOutput, { status: -2, location: [0, rows.badOutputLeaf, -1] });
assert.deepEqual(node.rejectedLibrary, { status: -2, location: [0, rows.badLibraryLeaf, -1] });
assert.deepEqual(node.imports, [
    { module: "standardwasm:resources", name: "memory", kind: "memory" },
    { module: "standardwasm:com", name: "stdcall:i32(p32,u32,p32,p32)", kind: "function" },
    { module: "standardwasm:com", name: "coCreateInstance", kind: "function" }
]);
const server = await serveLaunch(0, compilerPath);
let browser;
try {
    browser = await chromium.launch({ channel: "msedge" });
    const page = await browser.newPage();
    await page.route("**/com-activation.pack", route => route.fulfill({ body: Buffer.from(pack), contentType: "application/octet-stream" }));
    await page.goto(server.url);
    const edge = await page.evaluate(async ({ code, rows, ids }) => {
        const { instantiateManagedModule } = await import("/launch/resource-host.mjs");
        const assets = await Promise.all(["/StandardWasm.Compiler.wasm", "/com-activation.pack"].map(async path => {
            const response = await fetch(path);
            if (!response.ok) throw Error(`${path} returned HTTP ${response.status}`);
            return response.arrayBuffer();
        }));
        return (0, eval)(`(${code})`)(...assets, rows, ids, instantiateManagedModule);
    }, { code: exercise.toString(), rows, ids });
    assert.deepEqual(edge, node);
    const sha256 = bytes => createHash("sha256").update(bytes).digest("hex");
    const results = { compiler: sha256(compiler), assembly: sha256(readFileSync(assembly)), clr,
        node: process.version, edge: browser.version(), ...node };
    writeFileSync(join(evidence, "results.json"), JSON.stringify(results, null, 2) + "\n");
    console.log(JSON.stringify(results));
} finally {
    await browser?.close();
    await server.close();
}
