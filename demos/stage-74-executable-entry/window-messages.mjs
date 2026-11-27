const firstRegisteredMessage = 0xc000;
const lastRegisteredMessage = 0xffff;
const maximumNameLength = 255;

// Each launch has one virtual desktop. Keep IDs stable within that lifetime
// and independent from the host machine's registered messages.
export function createWindowMessageRegistration(memory, setError) {
    const ids = new Map();
    let nextId = firstRegisteredMessage;

    function fail(error) {
        setError(error);
        return 0;
    }

    return function registerWindowMessageW(pointer) {
        let at = pointer >>> 0;
        if (at === 0) return fail(87); // ERROR_INVALID_PARAMETER
        const view = new DataView(memory.buffer);
        let name = "";
        for (let length = 0; length <= maximumNameLength; length++, at += 2) {
            if (at + 2 > view.byteLength) return fail(998); // ERROR_NOACCESS
            let unit = view.getUint16(at, true);
            if (unit === 0) break;
            if (length === maximumNameLength) return fail(234); // ERROR_MORE_DATA
            // Win32 folds ASCII case in registered names. Preserve the other
            // UTF-16 units, including isolated surrogates and a leading BOM.
            if (unit >= 65 && unit <= 90) unit += 32;
            name += String.fromCharCode(unit);
        }

        if (!name) return 0;
        const ordinal = integerMessageName(name);
        if (ordinal !== null)
            return ordinal > 0 && ordinal < firstRegisteredMessage ? ordinal : fail(87);
        const existing = ids.get(name);
        if (existing !== undefined) return existing;
        if (nextId > lastRegisteredMessage) return fail(8); // ERROR_NOT_ENOUGH_MEMORY
        const id = nextId++;
        ids.set(name, id);
        return id;
    };
}

function integerMessageName(name) {
    if (name.length < 2 || name[0] !== "#") return null;
    let ordinal = 0;
    for (let index = 1; index < name.length; index++) {
        const digit = name.charCodeAt(index) - 48;
        if (digit < 0 || digit > 9) return null;
        // Native decimal accumulation wraps at the uint32 boundary.
        ordinal = (Math.imul(ordinal, 10) + digit) >>> 0;
    }
    return ordinal;
}
