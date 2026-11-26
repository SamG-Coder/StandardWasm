import { spawnSync } from "node:child_process";
import { mkdirSync, mkdtempSync, readFileSync, renameSync, rmSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

export const root = fileURLToPath(new URL("../", import.meta.url));
const driverProject = "tools/PortableCompiler.Driver";
const driver = join(root, driverProject, "bin/Release/net10.0/PortableCompiler.Driver.dll");
let driverReady = false;
export function run(command, args) {
    const result = spawnSync(command, args, { stdio: "inherit", cwd: root });
    if (result.error) throw result.error;
    if (result.status !== 0) throw Error(command + " exited with " + result.status);
}

export function invokeCompiler(dll, exportType, output, options = {}) {
    if (!driverReady) {
        run("dotnet", ["build", driverProject, "-c", "Release"]);
        driverReady = true;
    }
    return spawnSync("dotnet", [driver, resolve(dll), exportType, resolve(output)], { ...options, cwd: root });
}

export function compile(dll, exportType, output) {
    const target = resolve(output);
    mkdirSync(dirname(target), { recursive: true });
    const work = mkdtempSync(join(dirname(target), ".compile-"));
    try {
        const wasm = join(work, "module.wasm");
        const result = invokeCompiler(dll, exportType, wasm, { stdio: "inherit" });
        if (result.error) throw result.error;
        if (result.status !== 0) throw Error(`Portable compiler exited with ${result.status}`);
        const binary = readFileSync(wasm);
        if (!WebAssembly.validate(binary)) throw Error("Host engine rejected the module");
        // Publish only after the executing engine has validated the module.
        renameSync(wasm, target);
    } finally {
        rmSync(work, { recursive: true, force: true });
    }
}

