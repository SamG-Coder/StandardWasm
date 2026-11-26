import { readPe } from "../stage-73-executable-intake/loader/pe.mjs";

function pathKey(path) {
    return path.replaceAll("\\", "/").toLowerCase();
}

function findAsset(assets, declaredPath) {
    const key = pathKey(declaredPath);
    if (assets.has(key)) return assets.get(key);
    const file = key.split("/").at(-1);
    // The SDK can flatten package paths when publishing an apphost.
    const matches = [...assets].filter(([path]) => path.split("/").at(-1) === file);
    if (matches.length > 1) throw Error("Ambiguous managed companion: " + declaredPath);
    return matches[0]?.[1];
}

export function readManagedCompanions(selection, dependencies, runtimeTarget) {
    const target = dependencies.targets?.[runtimeTarget];
    if (!target || typeof target !== "object")
        throw Error("The dependency manifest does not contain the selected runtime target.");

    const companions = new Set();
    for (const library of Object.values(target)) {
        for (const path of Object.keys(library.runtime ?? {})) {
            if (!/\.(dll|exe)$/i.test(path)) continue;
            const asset = findAsset(selection.assets, path);
            if (!asset) throw Error("Add the original managed companion: " + path);
            if (asset === selection.entry) continue;
            validateManaged(asset, "Companion");
            companions.add(asset);
        }
    }
    for (const asset of selection.framework ?? []) {
        validateManaged(asset, "Framework asset");
        companions.add(asset);
    }
    return [...companions];
}

function validateManaged(asset, label) {
    if (!readPe(asset.bytes)?.managed)
        throw Error(label + " is not a managed .NET assembly: " + asset.path);
}

export function packEntry(owner, companions, entryRow, includeModuleOrigins = false) {
    const images = [owner, ...companions];
    const prefix = includeModuleOrigins ? 8 : 0;
    const header = includeModuleOrigins ? 8 : 4;
    const basenames = includeModuleOrigins ? images.map(asset => {
        if (asset.loadedFromBytes) return "";
        const name = asset.path.replaceAll("\\", "/").split("/").at(-1);
        if (!name || name === "." || name === ".." || /[:\0]/.test(name))
            throw Error("Expected a module basename.");
        return name;
    }) : [];
    const size = images.reduce((length, image, index) =>
        length + header + image.bytes.length + 2 * (basenames[index]?.length ?? 0), prefix);
    const bytes = new Uint8Array(size);
    const view = new DataView(bytes.buffer);
    if (includeModuleOrigins) view.setUint32(4, 0x314f4d53, true); // SMO1
    let cursor = prefix;
    for (let index = 0; index < images.length; index++) {
        const image = images[index].bytes;
        const name = basenames[index] ?? "";
        view.setUint32(cursor, image.length, true);
        if (includeModuleOrigins) view.setUint32(cursor + 4, name.length * 2, true);
        bytes.set(image, cursor + header);
        cursor += header + image.length;
        for (let unit = 0; unit < name.length; unit++) {
            view.setUint16(cursor, name.charCodeAt(unit), true);
            cursor += 2;
        }
    }
    const name = new TextEncoder().encode("run");
    const entries = new Uint8Array(8 + name.length);
    const manifest = new DataView(entries.buffer);
    manifest.setUint32(0, entryRow, true);
    manifest.setUint32(4, name.length, true);
    entries.set(name, 8);
    return [bytes, entries];
}
