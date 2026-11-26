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

    const compilerModule = await WebAssembly.compile(compilerBytes);
    if (WebAssembly.Module.imports(compilerModule).length)
        throw Error("Expected the standalone self-hosted StandardWasm compiler.");
    const { exports: compiler } = await WebAssembly.instantiate(compilerModule);
    const source = compiler.CreateBytes(selection.entry.bytes.length);
    selection.entry.bytes.forEach((value, index) => compiler.SetByte(source, index, value));
    const capacity = 1 << 20;
    const output = compiler.CreateBytes(capacity);
    const entryToken = selection.entryToken;
    // The PE reader verified the MethodDef token. The compiler takes its row.
    const length = compiler.Compile(source, entryToken & 0xffffff, output);
    const result = { entry: selection.entry.path, entryToken, target, runtimeTarget };
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
