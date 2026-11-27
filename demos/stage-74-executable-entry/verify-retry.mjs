import assert from "node:assert/strict";
import binaryen from "binaryen";
import { runManagedEntry } from "./launch.mjs";

const MiB = 1024 * 1024;

function emit(wat) {
    const module = binaryen.parseText(wat);
    try { return Array.from(module.emitBinary()); }
    finally { module.dispose(); }
}

// A small compiler double controls workspace outcomes independently of the
// actual compiler's current workspace needs. The actual publishes tested by
// the caller still use the ordinary self-hosted compiler.
function compiler(result, minimumCapacity, guest) {
    const data = guest.map(byte => "\\" + byte.toString(16).padStart(2, "0")).join("");
    return emit(`(module
      (memory 1)
      (data (i32.const 0) "${data}")
      (global $capacity (mut i32) (i32.const 0))
      (global $calls (mut i32) (i32.const 0))
      (global $uploads (mut i32) (i32.const 0))
      (func (export "BundleModuleOrigins") (result i32) (i32.const 1))
      (func (export "CreateBytes") (param $size i32) (result i32)
        (if (i32.lt_u (local.get $size) (i32.const ${MiB}))
          (then (global.set $uploads (i32.add (global.get $uploads) (i32.const 1)))))
        (local.get $size))
      (func (export "SetByte") (param i32 i32 i32))
      (func (export "ReadByte") (param i32 i32) (result i32) (i32.load8_u (local.get 1)))
      (func (export "CreateDiagnostic") (result i32) (i32.const 1))
      (func (export "ReadDiagnostic") (param i32) (param $index i32) (result i32)
        (select (global.get $capacity)
          (select (global.get $calls) (global.get $uploads) (i32.eq (local.get $index) (i32.const 1)))
          (i32.eqz (local.get $index))))
      (func (export "CompileBundleDetailed") (param i32 i32) (param $size i32) (param i32) (result i32)
        (global.set $capacity (local.get $size))
        (global.set $calls (i32.add (global.get $calls) (i32.const 1)))
        (select (i32.const -3) (i32.const ${result}) (i32.lt_u (local.get $size) (i32.const ${minimumCapacity})))))`);
}

export async function verifyRetries(page) {
    const guest = emit('(module (func (export "run") (result i32) (i32.const 37)))');
    const cases = [
        { name: "grows and runs", result: guest.length, minimumCapacity: 2 * MiB, expected: [37, null, null] },
        { name: "reports final diagnostic", result: -2, minimumCapacity: 2 * MiB, expected: [null, -2, [2 * MiB, 2, 2]] },
        { name: "stops at the cap", result: -3, minimumCapacity: 0, expected: [null, -3, [8 * MiB, 4, 2]] },
        { name: "does not retry a missing dependency", result: -4, minimumCapacity: 0, expected: [null, -4, [MiB, 1, 2]] }
    ];
    const compilers = cases.map(({ result, minimumCapacity }) => compiler(result, minimumCapacity, guest));
    const encode = value => Array.from(new TextEncoder().encode(JSON.stringify(value)));
    const runtimeTarget = ".NETCoreApp,Version=v10.0";
    const input = {
        entry: { path: "RetryFixture.dll", bytes: [77, 90] }, entryToken: 0x06000001,
        config: { bytes: encode({ runtimeOptions: { tfm: "net10.0" } }) },
        deps: { bytes: encode({ runtimeTarget: { name: runtimeTarget }, targets: { [runtimeTarget]: {} } }) }
    };
    const node = await Promise.all(compilers.map(bytes => runManagedEntry(Uint8Array.from(bytes), {
        ...input, entry: { ...input.entry, bytes: Uint8Array.from(input.entry.bytes) }, assets: new Map(),
        config: { bytes: Uint8Array.from(input.config.bytes) }, deps: { bytes: Uint8Array.from(input.deps.bytes) }
    })));
    const edge = await page.evaluate(async ({ input, compilers }) => {
        const { runManagedEntry } = await import("/launch/launch.mjs");
        const selection = {
            ...input, entry: { ...input.entry, bytes: Uint8Array.from(input.entry.bytes) }, assets: new Map(),
            config: { bytes: Uint8Array.from(input.config.bytes) }, deps: { bytes: Uint8Array.from(input.deps.bytes) }
        };
        return Promise.all(compilers.map(bytes => runManagedEntry(Uint8Array.from(bytes), selection)));
    }, { input, compilers });
    return cases.map(({ name, expected }, index) => {
        assert.deepEqual(edge[index], node[index], name);
        const { exitCode = null, compilerStatus = null, diagnostic = null } = node[index];
        assert.deepEqual([exitCode, compilerStatus, diagnostic], expected, name);
        return { name, exitCode, compilerStatus, diagnostic };
    });
}
