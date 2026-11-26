import { packEntry, readManagedCompanions } from "./companions.mjs";

const decoder = new TextDecoder("utf-8", { fatal: true });

function readManifest(asset) {
    if (!asset) throw Error("Provide the original .NET deployment manifests.");
    return JSON.parse(decoder.decode(asset.bytes));
}

export async function runManagedEntry(compilerBytes, selection) {
    const config = readManifest(selection.config);
    const deps = readManifest(selection.deps);
    const target = config.runtimeOptions?.tfm;
    const runtimeTarget = deps.runtimeTarget?.name;
    if (typeof target !== "string" || typeof runtimeTarget !== "string")
        throw Error("The original runtime configuration and dependency target are required.");

    const companions = readManagedCompanions(selection, deps, runtimeTarget);
    const compilerModule = await WebAssembly.compile(compilerBytes);
    if (WebAssembly.Module.imports(compilerModule).length)
        throw Error("Expected the standalone self-hosted StandardWasm compiler.");
    const { exports: compiler } = await WebAssembly.instantiate(compilerModule);
    function upload(bytes) {
        const source = compiler.CreateBytes(bytes.length);
        bytes.forEach((value, index) => compiler.SetByte(source, index, value));
        return source;
    }
    const capacity = 1 << 20;
    const output = compiler.CreateBytes(capacity);
    const entryToken = selection.entryToken;
    // The PE reader verified the MethodDef token. The compiler takes its row.
    const row = entryToken & 0xffffff;
    const result = { entry: selection.entry.path, entryToken, target, runtimeTarget };
    const origins = compiler.BundleModuleOrigins?.() === 1;
    let length;
    if (companions.length === 0 && !origins) length = compiler.Compile(upload(selection.entry.bytes), row, output);
    else {
        const [bundle, entries] = packEntry(selection.entry, companions, row, origins);
        const source = upload(bundle), manifest = upload(entries);
        if (typeof compiler.CompileBundleDetailed === "function") {
            const location = compiler.CreateDiagnostic();
            length = compiler.CompileBundleDetailed(source, manifest, output, location);
            if (length < 0) result.diagnostic = [0, 1, 2].map(index => compiler.ReadDiagnostic(location, index));
        } else if (typeof compiler.CompileBundle === "function") {
            length = compiler.CompileBundle(source, manifest, output);
        } else throw Error("This compiler build does not support managed assembly bundles.");
    }
    if (length < 0) return { ...result, state: "unsupported", compilerStatus: length };
    if (!length || length > capacity) throw Error("The self-hosted compiler returned an invalid module length.");

    const code = Uint8Array.from({ length }, (_, i) => compiler.ReadByte(output, i));
    const module = await WebAssembly.compile(code);
    if (WebAssembly.Module.imports(module).length)
        throw Error("This entry host does not provide runtime imports yet.");
    const { exports } = await WebAssembly.instantiate(module);
    if (typeof exports.run !== "function" || exports.run.length !== 0)
        throw Error("This entry host supports a parameterless managed entry point.");
    const exitCode = exports.run();
    if (!Number.isInteger(exitCode)) throw Error("This entry host expects a scalar integer exit result.");
    const digest = new Uint8Array(await crypto.subtle.digest("SHA-256", code));
    const moduleSha256 = Array.from(digest, value => value.toString(16).padStart(2, "0")).join("");
    return { ...result, state: "finished", exitCode, moduleBytes: length, moduleSha256 };
}
