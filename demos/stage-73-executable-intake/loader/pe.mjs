// PE data directories use RVAs, while the input is a file image.
function readLayout(bytes) {
    if (bytes.length < 64) return null;
    const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
    if (view.getUint16(0, true) !== 0x5a4d) return null;
    const pe = view.getUint32(60, true);
    if (pe > bytes.length - 24 || view.getUint32(pe, true) !== 0x4550) return null;
    const count = view.getUint16(pe + 6, true);
    const optionalLength = view.getUint16(pe + 20, true);
    const optional = pe + 24;
    const sections = optional + optionalLength;
    if (optionalLength < 2 || sections + count * 40 > bytes.length) return null;
    const magic = view.getUint16(optional, true);
    const directories = magic === 0x10b ? 96 : magic === 0x20b ? 112 : 0;
    if (!directories || optionalLength < directories) return null;
    const directoryCount = view.getUint32(optional + directories - 4, true);
    function locate(address, length) {
        for (let i = 0; i < count; i++) {
            const section = sections + i * 40;
            const relative = address - view.getUint32(section + 12, true);
            const rawLength = view.getUint32(section + 16, true);
            const offset = view.getUint32(section + 20, true) + relative;
            if (relative >= 0 && relative + length <= rawLength && offset + length <= bytes.length)
                return offset;
        }
        return -1;
    }

    function directory(index) {
        if (index >= directoryCount) return [];
        const offset = directories + index * 8;
        if (offset + 8 > optionalLength) return null;
        const at = optional + offset;
        return [view.getUint32(at, true), view.getUint32(at + 4, true)];
    }

    return { view, directory, locate };
}

export function readPe(bytes) {
    const layout = readLayout(bytes);
    if (!layout) return null;
    const { view, directory, locate } = layout;
    const cli = directory(14);
    if (!cli) return null;
    if (!cli.length) return { managed: false };
    const [rva, size] = cli;
    if (!rva && !size) return { managed: false };
    if (!rva || size < 72) return null;

    const header = locate(rva, 72);
    if (header < 0 || view.getUint32(header, true) < 72) return null;
    const metadataSize = view.getUint32(header + 12, true);
    const metadata = locate(view.getUint32(header + 8, true), metadataSize);
    if (metadataSize < 4 || metadata < 0 || view.getUint32(metadata, true) !== 0x424a5342) return null;
    const flags = view.getUint32(header + 16, true);
    const token = view.getUint32(header + 20, true);
    return { managed: true, entryToken: (flags & 0x10) === 0 && token >>> 24 === 6 ? token : 0 };
}


// The activation context can refer to a numeric resource in an original PE
// image. Directory links are relative to the resource root. Payload addresses
// are RVAs. Return only the exact language requested by the caller.
export function readPeResource(bytes, type, id, language) {
    if (![type, id, language].every(value => Number.isInteger(value) && value >= 0 && value <= 0xffff))
        return null;
    const layout = readLayout(bytes);
    if (!layout) return null;
    const { view, directory, locate } = layout;
    const resource = directory(2);
    if (!resource?.length) return null;
    const [rva, size] = resource;
    const base = rva && size >= 16 ? locate(rva, size) : -1;
    if (base < 0) return null;

    function child(table, key, subtree) {
        if (table < 0 || table > size - 16) return -1;
        const at = base + table;
        const named = view.getUint16(at + 12, true);
        const numeric = view.getUint16(at + 14, true);
        if ((named + numeric) * 8 > size - table - 16) return -1;
        for (let index = named; index < named + numeric; index++) {
            const entry = at + 16 + index * 8;
            if (view.getUint32(entry, true) !== key) continue;
            const target = view.getUint32(entry + 4, true);
            if ((target >>> 31) !== (subtree ? 1 : 0)) return -1;
            return target & 0x7fffffff;
        }
        return -1;
    }

    const typeTable = child(0, type, true);
    const languageTable = child(typeTable, id, true);
    const descriptor = child(languageTable, language, false);
    if (descriptor < 0 || descriptor > size - 16) return null;
    const length = view.getUint32(base + descriptor + 4, true);
    const data = locate(view.getUint32(base + descriptor, true), length);
    return data < 0 ? null : bytes.subarray(data, data + length);
}
