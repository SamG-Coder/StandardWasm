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

export function packEntry(owner, companions, entryRow) {
    const images = [owner, ...companions].map(asset => asset.bytes);
    const bytes = new Uint8Array(images.reduce((length, image) => length + 4 + image.length, 0));
    const view = new DataView(bytes.buffer);
    let cursor = 0;
    for (const image of images) {
        view.setUint32(cursor, image.length, true);
        bytes.set(image, cursor + 4);
        cursor += 4 + image.length;
    }
    const name = new TextEncoder().encode("run");
    const entries = new Uint8Array(8 + name.length);
    const manifest = new DataView(entries.buffer);
    manifest.setUint32(0, entryRow, true);
    manifest.setUint32(4, name.length, true);
    entries.set(name, 8);
    return [bytes, entries];
}
