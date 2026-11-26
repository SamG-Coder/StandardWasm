import { packEntry } from "../../stage-74-executable-entry/companions.mjs";
import { readWebcil } from "./webcil.mjs";

const decoder = new TextDecoder("utf-8", { fatal: true });

function readBootConfiguration(script) {
    // The .NET 10 SDK marks the JSON embedded in the fingerprinted dotnet loader.
    const source = decoder.decode(script.bytes);
    const start = "/*json-start*/", end = "/*json-end*/";
    const first = source.indexOf(start), last = source.indexOf(end, first + start.length);
    if (first < 0 || last < 0 || source.indexOf(start, first + start.length) !== -1 ||
        source.indexOf(end) !== last || source.indexOf(end, last + end.length) !== -1)
        throw Error("The .NET 10 SDK boot configuration was not found.");
    return JSON.parse(source.slice(first + start.length, last));
}

async function sha256(bytes) {
    const digest = new Uint8Array(await crypto.subtle.digest("SHA-256", bytes));
    return "sha256-" + btoa(String.fromCharCode(...digest));
}

export async function openStandalonePublish(files) {
    const assets = new Map();
    for (const file of files) {
        const path = file.path.replaceAll("\\", "/");
        if (path.includes(":") || path.includes("\0") ||
            path.split("/").some(part => !part || part === "." || part === ".."))
            throw Error("Invalid publish file path: " + file.path);
        if (assets.has(path)) throw Error("Duplicate publish file: " + path);
        assets.set(path, file);
    }

    // Accept either a wwwroot selection or a publish folder with its wwwroot.
    const loaders = [...assets.keys()].filter(path =>
        /^(?:wwwroot\/)?_framework\/dotnet(?:\.[a-z0-9]+)?\.js$/.test(path));
    if (loaders.length !== 1) throw Error("Provide one standalone .NET 10 publish folder.");
    const loader = loaders[0], framework = loader.slice(0, loader.lastIndexOf("/") + 1);
    const boot = readBootConfiguration(assets.get(loader));
    if (typeof boot.mainAssemblyName !== "string" || !Array.isArray(boot.resources?.coreAssembly) ||
        !Array.isArray(boot.resources?.assembly))
        throw Error("The boot configuration does not declare a standalone managed entry and assemblies.");

    const managed = new Map();
    for (const file of [...boot.resources.coreAssembly, ...boot.resources.assembly]) {
        if (typeof file?.name !== "string" || !/^[^/\\:\0]+\.wasm$/.test(file.name) ||
            typeof file.virtualPath !== "string" || !/^[^/\\:\0]+\.wasm$/.test(file.virtualPath))
            throw Error("Expected file names for SDK Webcil assembly assets.");
        if (managed.has(file.virtualPath)) throw Error("Duplicate boot assembly: " + file.virtualPath);
        const path = framework + file.name, asset = assets.get(path);
        if (!asset) throw Error("Add the original boot assembly: " + path);
        const bytes = readWebcil(asset.bytes);
        if (await sha256(asset.bytes) !== file.hash) throw Error("Boot assembly digest mismatch: " + path);
        managed.set(file.virtualPath, { path, bytes, hash: file.hash, original: asset });
    }

    const entry = managed.get(boot.mainAssemblyName + ".wasm");
    if (!entry) throw Error("The declared managed entry is missing from the boot assemblies.");
    return { entry, companions: [...managed.values()].filter(asset => asset !== entry), assets };
}

export async function inspectStandaloneEntry(compilerBytes, selection) {
    const module = await WebAssembly.compile(compilerBytes);
    if (WebAssembly.Module.imports(module).length) throw Error("Expected the standalone StandardWasm compiler.");
    const { exports: compiler } = await WebAssembly.instantiate(module);
    if (typeof compiler.ReadWebcilEntryToken !== "function")
        throw Error("This StandardWasm compiler does not read Webcil entry points.");

    function upload(bytes) {
        const source = compiler.CreateBytes(bytes.length);
        bytes.forEach((value, index) => compiler.SetByte(source, index, value));
        return source;
    }

    const entryToken = compiler.ReadWebcilEntryToken(upload(selection.entry.bytes));
    if (entryToken < 0) throw Error("No managed Webcil MethodDef entry point was found.");
    const [bundle, manifest] = packEntry(selection.entry, selection.companions, entryToken & 0xffffff);
    const location = compiler.CreateDiagnostic();
    const output = compiler.CreateBytes(1 << 20);
    const compilerStatus = compiler.CompileBundleDetailed(upload(bundle), upload(manifest), output, location);
    const diagnostic = compilerStatus < 0
        ? [0, 1, 2].map(index => compiler.ReadDiagnostic(location, index))
        : null;
    const assemblies = [selection.entry, ...selection.companions];
    return {
        entry: selection.entry.path, entryToken, managedAssemblies: assemblies.length, compilerStatus, diagnostic,
        diagnosticAssembly: assemblies[diagnostic?.[0]]?.path ?? null
    };
}
