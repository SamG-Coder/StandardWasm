const wasmHeader = [0, 97, 115, 109, 1, 0, 0, 0];
const webcilMagic = 0x4c496257;

// Read passive data from the SDK wrapper without loading its executable sections.
export function readWebcil(bytes) {
    if (bytes.length < wasmHeader.length || !wasmHeader.every((value, index) => bytes[index] === value))
        throw Error("Expected a Webcil WebAssembly wrapper.");

    const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
    let cursor = wasmHeader.length;
    function readSize(limit) {
        let size = 0;
        for (let index = 0; index < 5 && cursor < limit; index++) {
            const byte = bytes[cursor++];
            if (index === 4 && byte > 15) break;
            size += (byte & 127) * 2 ** (index * 7);
            if (byte < 128) return size;
        }
        throw Error("Invalid Webcil wrapper length.");
    }

    let payload;
    while (cursor < bytes.length) {
        const id = bytes[cursor++];
        const length = readSize(bytes.length);
        if (length > bytes.length - cursor) throw Error("Truncated Webcil wrapper section.");
        const end = cursor + length;
        if (id !== 11) {
            cursor = end;
            continue;
        }
        if (payload || readSize(end) !== 2 || readSize(end) !== 1)
            throw Error("Expected two passive Webcil data segments.");
        const sizeLength = readSize(end);
        if (sizeLength < 4 || sizeLength > end - cursor)
            throw Error("Invalid Webcil payload length segment.");
        const advertised = view.getUint32(cursor, true);
        cursor += sizeLength;
        if (readSize(end) !== 1) throw Error("Expected a passive Webcil payload.");
        const size = readSize(end);
        if (size !== advertised || size < 28 || size !== end - cursor || cursor % 4 !== 0 ||
            view.getUint32(cursor, true) !== webcilMagic)
            throw Error("Invalid Webcil payload.");
        payload = bytes.subarray(cursor, end);
        cursor = end;
    }
    if (!payload) throw Error("WebAssembly module contains no managed Webcil payload.");
    return payload;
}
