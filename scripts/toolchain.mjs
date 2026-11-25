import { spawnSync } from "node:child_process";
import { mkdirSync, mkdtempSync, readFileSync, renameSync, rmSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

export const root = fileURLToPath(new URL("../", import.meta.url));
export function run(command, args) {
    const result = spawnSync(command, args, { stdio: "inherit", cwd: root });
    if (result.error) throw result.error;
    if (result.status !== 0) throw Error(command + " exited with " + result.status);
}

export function compile(dll, exportType, output) {
    const target = resolve(output);
    mkdirSync(dirname(target), { recursive: true });
    const work = mkdtempSync(join(dirname(target), ".compile-"));
    try {
        const wat = join(work, "module.wat");
        run("dotnet", ["run", "--project", "Compiler", "-c", "Release", "--", resolve(dll), exportType, wat]);
        const wasm = join(work, "module.wasm");
        run(process.execPath, ["node_modules/binaryen/bin/wasm-as", wat,
            "--enable-gc", "--enable-reference-types", "--enable-tail-call", "-o", wasm]);
        const binary = readFileSync(wasm);
        if (!WebAssembly.validate(binary)) throw Error("Host engine rejected the module");
        // Publish only after both the assembler and the executing engine have validated the module.
        renameSync(wasm, target);
    } finally {
        rmSync(work, { recursive: true, force: true });
    }
}

