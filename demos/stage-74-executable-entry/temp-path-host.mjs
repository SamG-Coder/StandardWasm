const maxPathLength = 260;
const noAccess = 998;

// The virtual filesystem provider owns this directory and its lifetime.
// This adapter only implements the UTF-16 path query against guest memory.
export function createTempPathHost(memory, directory, errors) {
    if (typeof directory !== "string" || directory.length > maxPathLength ||
        !/^[A-Za-z]:\\(?:[^\\/:*?"<>|\u0000-\u001f]+\\)*$/u.test(directory) ||
        directory.slice(3, -1).split("\\").some(part => part.endsWith(".") || part.endsWith(" ")))
        throw new TypeError("StandardWasm: expected a canonical virtual temp directory");

    const required = directory.length + 1;

    function getTempPathW(capacity, pointer, availableUnits = -1) {
        const count = capacity >>> 0;
        const address = pointer >>> 0;
        if (count === 0) return required;

        const buffer = memory.buffer;
        const bytes = count * 2;
        if (!address || bytes > buffer.byteLength - address || (availableUnits >= 0 && count > availableUnits)) {
            errors.system = noAccess;
            return 0;
        }

        // Both Windows exports clear the caller's buffer when the canonical
        // (already slash-terminated) path does not fit, and pad it on success.
        new Uint8Array(buffer, address, bytes).fill(0);
        if (count < required) return required;

        const characters = new DataView(buffer, address, bytes);
        for (let i = 0; i < directory.length; i++)
            characters.setUint16(i * 2, directory.charCodeAt(i), true);
        return directory.length;
    }

    return {
        getTempPathW,
        getTempPath2W(capacity, pointer, availableUnits) {
            // The current Windows export clears the thread's system error
            // for successful and short-buffer queries.
            errors.system = 0;
            return getTempPathW(capacity, pointer, availableUnits);
        }
    };
}
