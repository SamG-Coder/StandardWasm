import { packEntry, readManagedCompanions } from "./companions.mjs";
import { frameworkInventory, frameworkMissingExitCode, frameworkMissingMessage, resolveFrameworks } from "./frameworks.mjs";
import { validName } from "./long-path-host.mjs";
import { instantiateManagedModule } from "./resource-host.mjs";
import { foldWin32Name } from "./win32-upcase.mjs";

const decoder = new TextDecoder("utf-8", { fatal: true });
// Stage 198: the virtual directory that holds the dropped executable.
export const virtualExecutableDirectory = "C:\\StandardWasm\\App";

// Issue #275: the launch directory as Win32 path APIs see it. The mount lists
// exactly the files the user dropped, at their publish-relative paths. Files
// extracted from a single-file bundle and supplied framework assets are not on
// disk beside a Windows apphost, so they are left out. The virtual volume has no
// 8.3 names, as on a volume with short-name creation disabled. The running
// executable and entry assembly are mapped images, so deleting them is refused.
export function launchFileSystem(selection, directory = virtualExecutableDirectory) {
    const entries = [], kinds = new Map();
    const inUse = new Set([selection.executablePath, selection.entry?.path].filter(Boolean));
    for (const asset of selection.assets?.values?.() ?? []) {
        if (asset.loadedFromBytes || typeof asset.path !== "string") continue;
        const names = asset.path.replaceAll("\\", "/").split("/");
        // Windows cannot hold a name the mount rejects, so it is not on the volume.
        if (!names.every(validName)) continue;
        const keys = names.map((_, depth) => names.slice(0, depth + 1).map(foldWin32Name).join("/"));
        // A file and a folder cannot share one name in a directory; the first dropped one wins.
        if (kinds.has(keys.at(-1)) || keys.slice(0, -1).some(key => kinds.get(key) === "file")) continue;
        keys.forEach((key, depth) => {
            const kind = depth === keys.length - 1 ? "file" : "directory";
            if (kinds.has(key)) return;
            kinds.set(key, kind);
            entries.push({ path: names.slice(0, depth + 1).join("/"), kind,
                ...(kind === "file" && inUse.has(asset.path) ? { readOnly: true } : {}) });
        });
    }
    return { currentDirectory: directory, longPathMount: { root: directory, entries } };
}

function readManifest(asset) {
    if (!asset) throw Error("Provide the original .NET deployment manifests.");
    return JSON.parse(decoder.decode(asset.bytes));
}

export async function runManagedEntry(compilerBytes, selection, platform) {
    const config = readManifest(selection.config);
    const deps = readManifest(selection.deps);
    const target = config.runtimeOptions?.tfm;
    const runtimeTarget = deps.runtimeTarget?.name;
    if (typeof target !== "string" || typeof runtimeTarget !== "string")
        throw Error("The original runtime configuration and dependency target are required.");
    // Issue #299: as hostfxr, resolve the shared frameworks before anything is compiled.
    const frameworks = resolveFrameworks(config, frameworkInventory(selection));
    if (!frameworks.ok) return {
        entry: selection.entry.path, entryToken: selection.entryToken, target, runtimeTarget,
        state: "framework-missing", exitCode: frameworkMissingExitCode, missingFramework: frameworks.missing,
        stderr: frameworkMissingMessage(frameworks, selection.entry.path)
    };

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
    const entryToken = selection.entryToken;
    // The PE reader verified the MethodDef token. The compiler takes its row.
    const row = entryToken & 0xffffff;
    const result = { entry: selection.entry.path, entryToken, target, runtimeTarget };
    const origins = compiler.BundleModuleOrigins?.() === 1;
    let compile, location;
    if (companions.length === 0 && !origins) {
        const source = upload(selection.entry.bytes);
        compile = output => compiler.Compile(source, row, output);
    } else {
        const [bundle, entries] = packEntry(selection.entry, companions, row, origins);
        const source = upload(bundle), manifest = upload(entries);
        if (typeof compiler.CompileBundleDetailed === "function") {
            location = compiler.CreateDiagnostic();
            compile = output => compiler.CompileBundleDetailed(source, manifest, output, location);
        } else if (typeof compiler.CompileBundle === "function") {
            compile = output => compiler.CompileBundle(source, manifest, output);
        } else throw Error("This compiler build does not support managed assembly bundles.");
    }

    // The compiler sizes several working tables from this capacity. Retry
    // workspace exhaustion with the same uploaded inputs and cap browser memory use.
    const maximumCapacity = 8 * 1024 * 1024;
    let capacity = 1024 * 1024;
    let output = compiler.CreateBytes(capacity);
    let length = compile(output);
    while (length === -3 && capacity < maximumCapacity) {
        capacity *= 2;
        output = compiler.CreateBytes(capacity);
        length = compile(output);
    }
    if (length < 0) {
        if (location) result.diagnostic = [0, 1, 2].map(index => compiler.ReadDiagnostic(location, index));
        return { ...result, state: "unsupported", compilerStatus: length };
    }
    if (!length || length > capacity) throw Error("The self-hosted compiler returned an invalid module length.");

    const code = Uint8Array.from({ length }, (_, i) => compiler.ReadByte(output, i));
    const module = await WebAssembly.compile(code);
    // File input exposes no Windows host path. Each launched program gets a
    // virtual working directory unless its platform supplies another one.
    const includedFrameworks = config.runtimeOptions?.includedFrameworks;
    const isDesktop = framework => framework?.name === "Microsoft.WindowsDesktop.App";
    const win32Desktop = frameworks.frameworks.some(isDesktop) ||
        (Array.isArray(includedFrameworks) && includedFrameworks.some(isDesktop));
    const services = { win32FileSystem: launchFileSystem(selection), win32Desktop, ...platform };
    const { exports } = await instantiateManagedModule(module, compiler, [selection.entry, ...companions],
        {}, selection.executablePath, undefined, undefined, services);
    if (typeof exports.run !== "function" || exports.run.length !== 0)
        throw Error("This entry host supports a parameterless managed entry point.");
    const exitCode = exports.run();
    if (!Number.isInteger(exitCode)) throw Error("This entry host expects a scalar integer exit result.");
    const digest = new Uint8Array(await crypto.subtle.digest("SHA-256", code));
    const moduleSha256 = Array.from(digest, value => value.toString(16).padStart(2, "0")).join("");
    return { ...result, state: "finished", exitCode, moduleBytes: length, moduleSha256 };
}
