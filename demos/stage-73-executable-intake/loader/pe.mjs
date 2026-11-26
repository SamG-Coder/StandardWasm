// The CLR directory uses RVAs, while the input is a file image.
export function readPe(bytes) {
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
    if (directoryCount <= 14) return { managed: false };
    if (optionalLength < directories + 15 * 8) return null;
    const cli = optional + directories + 14 * 8;
    const rva = view.getUint32(cli, true);
    const size = view.getUint32(cli + 4, true);
    if (!rva && !size) return { managed: false };
    if (!rva || size < 72) return null;

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

    const header = locate(rva, 72);
    if (header < 0 || view.getUint32(header, true) < 72) return null;
    const metadataSize = view.getUint32(header + 12, true);
    const metadata = locate(view.getUint32(header + 8, true), metadataSize);
    if (metadataSize < 4 || metadata < 0 || view.getUint32(metadata, true) !== 0x424a5342) return null;
    const flags = view.getUint32(header + 16, true);
    const token = view.getUint32(header + 20, true);
    return { managed: true, entryToken: (flags & 0x10) === 0 && token >>> 24 === 6 ? token : 0 };
}
