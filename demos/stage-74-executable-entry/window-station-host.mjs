const userObjectFlags = 1, flagsSize = 12, visibleFlag = 1;
const notEnoughMemory = 8, insufficientBuffer = 122, noAccess = 998;

// Borrowed station tokens are distinct from the owned thread handles and
// dynamic-library tokens. Each launch keeps one token for its lifetime.
let nextStation = 0x80000000;
const lastStation = 0x8fffffff;

export function createWindowStationHost(memory, desktop, setError) {
    let station = 0;

    function fail(error) {
        setError(error);
        return 0;
    }

    function getProcessWindowStation() {
        if (station === 0) {
            if (nextStation > lastStation) return fail(notEnoughMemory);
            station = nextStation++;
        }
        return station | 0;
    }

    function getUserObjectInformationW(handle, index, outputPointer, byteCount, neededPointer) {
        if (station === 0 || (handle >>> 0) !== station || index <= 0) return 0;
        if (index !== userObjectFlags)
            throw new RangeError("GetUserObjectInformationW supports UOI_FLAGS for the browser window station.");

        const view = new DataView(memory.buffer);
        const needed = neededPointer >>> 0;
        if (needed !== 0 && needed > view.byteLength - 4) return fail(noAccess);
        if ((byteCount >>> 0) < flagsSize) {
            if (needed !== 0) view.setUint32(needed, flagsSize, true);
            return fail(insufficientBuffer);
        }

        const output = outputPointer >>> 0;
        if (output === 0 || output > view.byteLength - flagsSize) return fail(noAccess);
        // USEROBJECTFLAGS has two four-byte BOOLs and one DWORD. The
        // desktop capability belongs to this launch, independent of tab focus.
        view.setUint32(output, 0, true);
        view.setUint32(output + 4, 0, true);
        view.setUint32(output + 8, desktop ? visibleFlag : 0, true);
        if (needed !== 0) view.setUint32(needed, flagsSize, true);
        return 1;
    }

    return { getProcessWindowStation, getUserObjectInformationW };
}
