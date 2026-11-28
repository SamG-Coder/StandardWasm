// Decision 0104: the host side of System.Runtime.InteropServices.NativeMemory.
//
// The compiled module imports `standardwasm:native` alloc(size, alignment, zero) -> address,
// free(address) and realloc(address, size, alignment) -> address. Blocks live in memory 0,
// the memory the host lends the module (decision 0060), in pages this heap grows itself, so
// they never overlap the module's shadow stack (decision 0065) or resource pages.
//
// Layout: every block is a power-of-two size class of at least 32 bytes. The address handed
// out is aligned to the requested alignment, and the 8 bytes before it record the block start
// and its size class. Freed blocks go to a per-class free list. Address 0 is never returned
// except to report exhaustion, which the module turns into a trap (OutOfMemoryException on
// the CLR).
// The launcher calls group[attachMemory](memory) once the module's memory 0 exists.
export const attachMemory = Symbol("standardwasm native heap memory");
const pageBytes = 65536;
const smallestClass = 5; // 32 bytes

export function createNativeHeap() {
    let memory = null;
    let view = null;
    let cursor = 0, limit = 0; // bump region inside the pages this heap grew
    const freeLists = [];

    function words() {
        if (view === null || view.buffer !== memory.buffer) view = new DataView(memory.buffer);
        return view;
    }

    function take(sizeClass) {
        const list = freeLists[sizeClass];
        if (list && list.length) return list.pop();
        const bytes = 2 ** sizeClass;
        if (limit - cursor < bytes) {
            const pages = Math.ceil(bytes / pageBytes);
            let first;
            try { first = memory.grow(pages); } catch { return 0; }
            // A fresh region; any rest of the previous one is abandoned.
            cursor = first * pageBytes;
            limit = cursor + pages * pageBytes;
            if (cursor === 0) cursor = 16; // address 0 stays null
        }
        const block = cursor;
        cursor += bytes;
        return block;
    }

    function alloc(size, alignment, zero) {
        size >>>= 0;
        alignment = Math.max(alignment >>> 0, 8);
        if (size === 0) size = 1; // .NET never returns null for a zero-byte request
        const needed = size + alignment + 8;
        if (needed > 2 ** 32) return 0;
        const sizeClass = Math.max(smallestClass, Math.ceil(Math.log2(needed)));
        const block = take(sizeClass);
        if (block === 0) return 0;
        const address = Math.ceil((block + 8) / alignment) * alignment;
        const header = words();
        header.setUint32(address - 8, block, true);
        header.setUint32(address - 4, sizeClass, true);
        if (zero) new Uint8Array(memory.buffer, address, size).fill(0);
        return address;
    }

    function usable(address) {
        const header = words();
        const block = header.getUint32(address - 8, true);
        return block + 2 ** header.getUint32(address - 4, true) - address;
    }

    function free(address) {
        address >>>= 0;
        if (address === 0) return;
        const header = words();
        const sizeClass = header.getUint32(address - 4, true);
        (freeLists[sizeClass] ??= []).push(header.getUint32(address - 8, true));
    }

    function realloc(address, size, alignment) {
        address >>>= 0;
        size >>>= 0;
        if (address === 0) return alloc(size, alignment, 0);
        const wanted = size === 0 ? 1 : size;
        const available = usable(address);
        if (wanted <= available && address % Math.max(alignment >>> 0, 8) === 0) return address;
        const moved = alloc(wanted, alignment, 0);
        if (moved === 0) return 0;
        // Bytes past the old size are unspecified on the CLR; copying the old block's usable
        // bytes up to the new size preserves every byte the program wrote.
        new Uint8Array(memory.buffer).copyWithin(moved, address, address + Math.min(available, wanted));
        free(address);
        return moved;
    }

    return {
        alloc, free, realloc,
        [attachMemory](value) {
            memory = value;
            view = null;
        }
    };
}
