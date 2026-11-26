// .NET HostModel stores the signed 64-bit manifest offset eight bytes before
// this marker in both regular and single-file apphosts.
const marker = Uint8Array.of(
    0x8b, 0x12, 0x02, 0xb9, 0x6a, 0x61, 0x20, 0x38,
    0x72, 0x7b, 0x93, 0x02, 0x14, 0xd7, 0xa0, 0x32,
    0x13, 0xf5, 0xb9, 0xe6, 0xef, 0xae, 0x33, 0x18,
    0xee, 0x3b, 0x2d, 0xce, 0x24, 0xb3, 0x6a, 0xae);
const decoder = new TextDecoder("utf-8", { fatal: true });

export function bundleMarker(bytes) {
    let found = -1;
    for (let i = bytes.indexOf(marker[0], 8); i >= 0 && i <= bytes.length - marker.length;
        i = bytes.indexOf(marker[0], i + 1)) {
        if (!marker.every((value, j) => bytes[i + j] === value)) continue;
        if (found >= 0) return -1;
        found = i;
    }
    return found;
}

export function bundleOffset(bytes, at) {
    const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
    const offset = view.getBigInt64(at - 8, true);
    if (offset !== 0n && (offset < BigInt(at + marker.length) || offset >= BigInt(bytes.length)))
        throw Error("Invalid .NET bundle manifest offset.");
    return Number(offset);
}

export function readBundle(bytes, offset, payloadStart) {
    const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
    let cursor = offset;
    function field(width) {
        const start = cursor;
        cursor += width;
        if (cursor > bytes.length) throw Error("Truncated .NET bundle manifest.");
        return start;
    }
    function int64() {
        const value = view.getBigInt64(field(8), true);
        if (value < 0n || value > BigInt(Number.MAX_SAFE_INTEGER)) throw Error("Invalid .NET bundle field.");
        return Number(value);
    }
    function string() {
        // The BinaryWriter length is a seven-bit encoded, nonnegative int32.
        let length = 0;
        for (let i = 0; i < 5; i++) {
            const value = bytes[field(1)];
            if (i === 4 && value > 7) break;
            length += (value & 127) * 2 ** (7 * i);
            if (value < 128) {
                const start = field(length);
                return decoder.decode(bytes.subarray(start, start + length));
            }
        }
        throw Error("Invalid .NET bundle string length.");
    }

    const major = view.getUint32(field(4), true);
    const minor = view.getUint32(field(4), true);
    if (major !== 6 || minor !== 0) throw Error("Unsupported .NET bundle format: " + major + "." + minor);
    const count = view.getInt32(field(4), true);
    string(); // Bundle identifier.
    const depsOffset = int64(), depsSize = int64();
    const configOffset = int64(), configSize = int64();
    field(8); // Header flags.
    if (count < 0 || count > Math.floor((bytes.length - cursor) / 26))
        throw Error("Invalid .NET bundle entry count.");

    const entries = [];
    for (let i = 0; i < count; i++) {
        const start = int64(), size = int64(), compressed = int64();
        const kind = bytes[field(1)], path = string();
        if (compressed) throw Error("Compressed .NET bundle entries are not supported: " + path);
        if (start < payloadStart || start > offset || size > offset - start)
            throw Error("Invalid .NET bundle entry range: " + path);
        entries.push({ path, kind, start, size, bytes: bytes.subarray(start, start + size), loadedFromBytes: true });
    }

    function headerEntry(kind, start, size) {
        const matches = entries.filter(entry => entry.kind === kind);
        if (matches.length !== 1 || matches[0].start !== start || matches[0].size !== size)
            throw Error("Missing or inconsistent .NET bundle configuration.");
        return matches[0].path;
    }
    return {
        entries,
        deps: headerEntry(3, depsOffset, depsSize),
        config: headerEntry(4, configOffset, configSize)
    };
}
