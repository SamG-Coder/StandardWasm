import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";
import { runOn32BitClr, wasm32Runtime } from "../../scripts/runtime-packs.mjs";
import { root } from "../../scripts/toolchain.mjs";
import { serveLaunch } from "../stage-74-executable-entry/launch-server.mjs";
import { instantiateManagedModule } from "../stage-74-executable-entry/resource-host.mjs";
import { createWindowState } from "../stage-74-executable-entry/window-state.mjs";

const stage = join(root, "demos/stage-340-window-paint-delivery");
const evidence = join(root, "artifacts/stage-340-window-paint-delivery");
mkdirSync(evidence, { recursive: true });
const oraclePath = join(evidence, "oracle.json");
runOn32BitClr(wasm32Runtime(), join(stage, "Oracle"), join(stage, "Oracle/bin/Release/net10.0/Oracle.dll"),
    [join(stage, "operations.json"), oraclePath]);
const oracle = JSON.parse(readFileSync(oraclePath, "utf8"));
const compilerPath = process.env.STANDARDWASM_COMPILER_PATH || join(root, "artifacts/StandardWasm.Compiler.wasm");
const compiler = readFileSync(compilerPath), fixture = readFileSync(oracle.assembly);

// Runs in Node and, through its source text, in Edge. It must stay self-contained.
async function exercise(compiler, fixture, operations, instantiate, createWindows) {
    const { instance: { exports: tool } } = await WebAssembly.instantiate(compiler);
    const encoder = new TextEncoder();
    function upload(bytes) {
        const target = tool.CreateBytes(bytes.length);
        for (let i = 0; i < bytes.length; i++) tool.SetByte(target, i, bytes[i]);
        return target;
    }
    const source = upload(fixture);
    const find = name => tool.FindRow(source, upload(encoder.encode(name)));
    const names = ["ThreadId", "ExecutableModule", "Post", "Peek", "Invalidate", "Validate", "UpdateRect", "Begin", "End",
        "SystemError", "SetErrors", "FixedSizes", "FixedFieldAccess", "FixedElementAccess"];
    const entries = [];
    for (const name of names) {
        const bytes = encoder.encode(name);
        const row = find(name);
        if (row < 1) throw Error("Missing " + name);
        const record = new Uint8Array(8 + bytes.length);
        const view = new DataView(record.buffer);
        view.setUint32(0, row, true);
        view.setUint32(4, bytes.length, true);
        record.set(bytes, 8);
        entries.push(...record);
    }
    const buffer = tool.CreateBytes(65536), diagnostic = tool.CreateDiagnostic();
    const location = () => [0, 1, 2].map(index => tool.ReadDiagnostic(diagnostic, index));
    const count = tool.CompileModuleDetailed(source, upload(entries), buffer, diagnostic);
    if (count < 1) throw Error("Fixture compilation " + count + " at " + location());
    const module = new WebAssembly.Module(Uint8Array.from({ length: count }, (_, i) => tool.ReadByte(buffer, i)));
    const imports = WebAssembly.Module.imports(module).map(({ module, name }) => module + "/" + name).sort();
    for (const name of ["beginPaint", "endPaint", "getUpdateRect", "validateRect", "invalidateRect", "peekMessageW"]) {
        if (imports.filter(entry => entry === "standardwasm:win32/" + name).length !== 1)
            throw Error("Expected a single host import for " + name);
    }
    const rejected = { status: tool.CompileDetailed(source, find("RejectPaintPointee"), buffer, diagnostic), location: location() };
    if (rejected.status !== -2 || rejected.location.join() !== [0, find("WrongPaintPointee"), -1].join())
        throw Error("Expected a located diagnostic for the non-PAINTSTRUCT pointee");
    // Direct indexing on this managed local still exceeds the proven operation set.
    const managedLocalRow = find("FixedManagedLocal");
    const managedLocal = tool.CompileDetailed(source, managedLocalRow, buffer, diagnostic);
    if (managedLocal !== -2 || location().join() !== [0, managedLocalRow, 13].join())
        throw Error("Expected a located diagnostic for direct fixed-buffer indexing on a managed local");

    async function launch() {
        const windows = createWindows();
        const log = [];
        const eraseResults = { A: 0, B: 1, H: 0 };
        const handles = { zero: 0, thread: -1, invalid: -99 };
        // Same creation order, styles and client sizes as the Windows oracle.
        // Handles grow with creation, so B is above A in the host z-order.
        for (const [name, style, x, y] of [["A", 0x90000000, 40, 40], ["B", 0x90000000, 300, 40], ["H", 0x80000000, 40, 200]]) {
            const handle = windows.register(style | 0, (message, wParam, lParam) => {
                log.push({ window: name, message, wParam, lParam });
                return message === 0x0014 ? eraseResults[name] : 0;
            });
            windows.setWindowGeometry(handle, [x, y, x + 200, y + 100], [x, y, x + 200, y + 100]);
            handles[name] = handle;
        }
        let memory;
        const { exports: guest } = await instantiate(module, tool, [], {}, "Fixture.exe", windows, undefined, {
            configureNativeLibraries(registry) { memory = registry.memory; }
        });
        if (!guest.ExecutableModule() || !memory) throw Error("Missing the launch ABI memory");
        return { guest, windows, handles, log, eraseResults, scratch: memory.grow(1) * 65536, memory };
    }

    const { guest, windows, handles, log, eraseResults, scratch, memory } = await launch();
    const fixedAddress = scratch + 32768;
    const fixedView = new DataView(memory.buffer);
    // Seed Words[1] by its x86 offset so the guest proves its own address calculation.
    fixedView.setInt32(fixedAddress + 16, -1234567, true);
    const fixedBuffers = {
        sizes: guest.FixedSizes(),
        fieldOffsets: guest.FixedFieldAccess(fixedAddress),
        element: guest.FixedElementAccess(fixedAddress),
        tag: fixedView.getUint8(fixedAddress),
        afterText: fixedView.getInt16(fixedAddress + 8, true),
        afterWide: fixedView.getUint8(fixedAddress + 32),
        last: fixedView.getInt32(fixedAddress + 48, true)
    };
    const rect = scratch, message = scratch + 64, buffers = {};
    let nextBuffer = scratch + 128;
    const words = (address, length) => Array.from({ length }, (_, i) => new DataView(memory.buffer).getInt32(address + i * 4, true));
    const targetName = hwnd => hwnd === -1 ? "unchanged" : hwnd === 0 ? "zero" :
        Object.keys(handles).find(name => handles[name] === hwnd);

    function run(operation) {
        const view = new DataView(memory.buffer);
        for (let i = 0; i < 4; i++) view.setInt32(rect + i * 4, operation.rect?.[i] ?? -7, true);
        const rectPointer = operation.rect || operation.output ? rect : 0;
        const hwnd = handles[operation.target];
        guest.SetErrors(789);
        switch (operation.kind) {
            case "invalidate":
                return { result: guest.Invalidate(hwnd, rectPointer, operation.erase ?? 0), error: guest.SystemError() };
            case "validate":
                return { result: guest.Validate(hwnd, rectPointer), error: guest.SystemError() };
            case "update": {
                const result = guest.UpdateRect(hwnd, rectPointer, operation.erase ?? 0);
                return { result, error: guest.SystemError(), rect: operation.output ? words(rect, 4) : null };
            }
            case "erase-result":
                eraseResults[operation.target] = operation.payload;
                return { result: 1 };
            case "post-thread":
                return { result: guest.Post(guest.ThreadId(), 0x8051, operation.payload, -100, 1) };
            case "peek": {
                for (let i = 0; i < 8; i++) view.setInt32(message + i * 4, -1, true);
                const result = guest.Peek(message, hwnd, operation.minimum ?? 0, operation.maximum ?? 0, operation.flags ?? 0);
                const [target, msg, wParam, lParam, , , , guard] = words(message, 8);
                return { result, target: targetName(target), msg: msg >>> 0, wParam, lParam, guard };
            }
            case "begin": {
                const address = buffers[operation.buffer] ??= (nextBuffer += 64) - 64;
                new Uint8Array(memory.buffer, address, 64).fill(0xcd);
                const context = guest.Begin(hwnd, address);
                const error = guest.SystemError();
                const paint = words(address, 8);
                const reserved = Array.from(new Uint8Array(memory.buffer, address + 32, 32),
                    byte => byte.toString(16).padStart(2, "0").toUpperCase()).join("");
                return { context, expected: { live: context !== 0, stored: paint[0] === context, fErase: paint[1],
                    rcPaint: paint.slice(2, 6), fRestore: paint[6], fIncUpdate: paint[7], reserved, error } };
            }
            case "end":
                return { result: guest.End(hwnd, buffers[operation.buffer]), error: guest.SystemError() };
            case "destroy":
                return { result: Number(windows.unregister(hwnd)) };
        }
        throw Error("Unknown operation " + operation.kind);
    }

    const results = operations.map(operation => {
        log.length = 0;
        let expected = run(operation), context = 0;
        if (operation.kind === "begin") ({ context, expected } = expected);
        const messages = log.map(({ window, message, wParam, lParam }) => ({ window, message, lParam,
            context: message !== 0x0014 ? "none" : wParam === 0 ? "zero" : wParam === context ? "paint" : "other" }));
        return { expected, messages };
    });

    // Browser-host boundaries. Windows has no counterpart for these launch-local checks.
    guest.SetErrors(789);
    const boundaries = {
        desktopInvalidate: [guest.Invalidate(0, 0, 1), guest.SystemError()],
        desktopValidate: [guest.Validate(0, 0), guest.SystemError()],
        paintOutsideMemory: [guest.Begin(handles.A, memory.buffer.byteLength - 63), guest.SystemError()],
        nullPaint: [guest.End(handles.A, 0), guest.SystemError()],
        rectOutsideMemory: [guest.UpdateRect(handles.A, memory.buffer.byteLength - 15, 0), guest.SystemError()]
    };
    const parent = handles.A;
    const child = windows.register(0x50000000, () => 0, { parent });
    windows.setWindowGeometry(child, [0, 0, 10, 10], [0, 0, 10, 10]);
    boundaries.visibleChild = [guest.Invalidate(child, 0, 0), guest.SystemError()];
    boundaries.parentWithChild = [guest.Invalidate(parent, 0, 0), guest.SystemError()];
    windows.unregister(child);
    guest.Invalidate(parent, 0, 0);
    const second = await launch();
    boundaries.isolated = second.guest.Peek(second.scratch, 0, 0, 0, 0);
    return { results, rejected, imports, boundaries, fixedBuffers };
}

const operations = JSON.parse(readFileSync(join(stage, "operations.json"), "utf8"));
const node = await exercise(compiler, fixture, operations, instantiateManagedModule, createWindowState);
for (const [index, operation] of oracle.operations.entries()) {
    assert.deepEqual(operation.operation, operations[index], `Oracle operation ${index} matches the script`);
    assert.deepEqual(node.results[index], { expected: operation.expected, messages: operation.messages },
        `Windows parity for operation ${index} (${operation.operation.kind})`);
}
assert.equal(node.results.length, oracle.operations.length);
assert.deepEqual(node.fixedBuffers, oracle.fixedBuffers, "Fixed-buffer layout and pointer writes match the x86 CLR");
assert.deepEqual(node.boundaries, {
    desktopInvalidate: [0, 50], desktopValidate: [0, 50], paintOutsideMemory: [0, 998], nullPaint: [0, 998],
    rectOutsideMemory: [0, 998], visibleChild: [0, 50], parentWithChild: [0, 50], isolated: 0
});
const server = await serveLaunch(0, compilerPath);
const browser = await chromium.launch({ channel: "msedge" });
try {
    const page = await browser.newPage();
    await page.route("**/window-paint.dll", route => route.fulfill({ body: fixture, contentType: "application/octet-stream" }));
    await page.goto(server.url);
    const edge = await page.evaluate(async ({ source, operations }) => {
        const { instantiateManagedModule } = await import("/launch/resource-host.mjs");
        const { createWindowState } = await import("/launch/window-state.mjs");
        const assets = await Promise.all(["/StandardWasm.Compiler.wasm", "/window-paint.dll"].map(async path => {
            const response = await fetch(path);
            if (!response.ok) throw Error(path + " returned HTTP " + response.status);
            return new Uint8Array(await response.arrayBuffer());
        }));
        return (0, eval)("(" + source + ")")(...assets, operations, instantiateManagedModule, createWindowState);
    }, { source: exercise.toString(), operations });
    assert.deepEqual(edge, node);
    const sha256 = bytes => createHash("sha256").update(bytes).digest("hex");
    writeFileSync(join(evidence, "results.json"), JSON.stringify({
        compiler: sha256(compiler), fixture: sha256(fixture), clr: oracle.clr, node: process.version,
        edge: browser.version(), operations: oracle.operations, ...node
    }, null, 2) + "\n");
    console.log("Window paint delivery matched " + node.results.length + " Windows x86 operations in Node and Edge.");
} finally {
    await browser.close();
    await server.close();
}
