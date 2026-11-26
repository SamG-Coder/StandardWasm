import { bundleMarker, bundleOffset, readBundle } from "./bundle.mjs";
import { readPe } from "./pe.mjs";

function pathKey(path) {
    const relative = path.replaceAll("\\", "/");
    if (relative.includes(":") || relative.includes("\0") ||
        relative.split("/").some(part => !part || part === "." || part === ".."))
        throw Error("Invalid publish file path: " + path);
    return relative.toLowerCase();
}

function index(files) {
    const assets = new Map();
    for (const file of files) {
        const key = pathKey(file.path);
        if (assets.has(key)) throw Error("Duplicate publish file: " + file.path);
        assets.set(key, file);
    }
    return assets;
}

export function openExecutable(files, selected) {
    const initial = index(files);
    const launch = initial.get(pathKey(selected));
    if (!launch) throw Error("Selected launch file was not provided.");
    const pe = readPe(launch.bytes);
    if (!pe) throw Error("The selected file is not a supported Windows PE.");
    const stem = launch.path.replace(/\.(exe|dll)$/i, "");
    let entry = launch, assets = initial, packageType = "managed-pe";
    let deps = stem + ".deps.json", config = stem + ".runtimeconfig.json";
    const marker = pe.managed ? -1 : bundleMarker(launch.bytes);
    if (!pe.managed) {
        if (marker < 0) throw Error("The executable is not a recognized .NET apphost.");
        const offset = bundleOffset(launch.bytes, marker);
        if (offset !== 0) {
            packageType = "single-file";
            const bundle = readBundle(launch.bytes, offset, marker + 32);
            deps = bundle.deps;
            config = bundle.config;
            assets = index([...bundle.entries, ...files.filter(file => file !== launch)]);
            const entryPath = config.replace(/\.runtimeconfig\.json$/i, ".dll");
            entry = assets.get(pathKey(entryPath));
            if (entry?.kind !== 1) throw Error("The managed entry assembly is missing from the .NET bundle.");
        } else {
            packageType = "apphost";
            entry = assets.get(pathKey(stem + ".dll"));
            const required = [stem + ".dll", deps, config];
            const missing = required.filter(path => !assets.has(pathKey(path)));
            if (missing.length) throw Error("Add the original publish companions: " + missing.join(", "));
        }
    }
    const entryToken = entry && readPe(entry.bytes)?.entryToken;
    if (!entryToken) throw Error("No managed MethodDef entry point was found.");
    return {
        packageType, entry, entryToken, assets,
        deps: assets.get(pathKey(deps)),
        config: assets.get(pathKey(config))
    };
}
