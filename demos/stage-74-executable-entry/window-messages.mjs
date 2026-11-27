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

export function createThreadMessageQueue(memory, windows, setError) {
    const messages = [];
    const maximumPostedMessages = 10000;
    let seenDestructions = windows.getDestroyedWindowCount();

    function fail(error) {
        setError(error);
        return 0;
    }

    function pruneDestroyedTargets() {
        const count = windows.getDestroyedWindowCount();
        if (count === seenDestructions) return;
        seenDestructions = count;
        let live = 0;
        for (const entry of messages)
            if (entry.hwnd === 0 || windows.hasWindow(entry.hwnd)) messages[live++] = entry;
        messages.length = live;
    }

    function post(hwnd, message, wParam, lParam) {
        pruneDestroyedTargets();
        if (messages.length === maximumPostedMessages) return fail(1816); // ERROR_NOT_ENOUGH_QUOTA
        messages.push({ hwnd, message: message >>> 0, wParam, lParam, time: performance.now() >>> 0 });
        return 1;
    }

    return {
        postThreadMessageW(thread, message, wParam, lParam) {
            if ((thread >>> 0) !== (windows.getCurrentThreadId() >>> 0)) return fail(1444); // ERROR_INVALID_THREAD_ID
            return post(0, message, wParam, lParam);
        },
        postMessageW(hwnd, message, wParam, lParam) {
            hwnd >>>= 0;
            if (hwnd === 0xffff) return fail(50); // HWND_BROADCAST
            if (hwnd !== 0 && !windows.hasWindow(hwnd)) return fail(1400); // ERROR_INVALID_WINDOW_HANDLE
            // These system messages borrow caller memory and require synchronous dispatch.
            if (message === 0x000c || message === 0x000d || message === 0x004a) return fail(1159); // ERROR_MESSAGE_SYNC_ONLY
            return post(hwnd, message, wParam, lParam);
        },
        peekMessageW(pointer, window, minimum, maximum, flags) {
            const filter = window >>> 0;
            const threadOnly = filter === 0xffffffff;
            if (filter !== 0 && !threadOnly && !windows.hasWindow(filter)) return fail(1400);
            const queueStatus = flags >>> 16;
            if (queueStatus !== 0 && (queueStatus & 0x08) === 0) return 0; // QS_POSTMESSAGE
            pruneDestroyedTargets();
            const first = minimum >>> 0;
            const last = maximum >>> 0;
            const allMessages = first === 0 && last === 0;
            // A thread post of WM_QUIT follows the range filter like any other post.
            // A quit produced by PostQuitMessage is a separate queue operation.
            const index = messages.findIndex(({ hwnd, message }) =>
                (allMessages || (message >= first && message <= last)) &&
                (filter === 0 || (threadOnly ? hwnd === 0 : hwnd === filter || windows.isWindowChild(filter, hwnd))));
            if (index < 0) return 0;

            const at = pointer >>> 0;
            const view = new DataView(memory.buffer);
            if (at === 0 || at + 28 > view.byteLength) return fail(998); // ERROR_NOACCESS
            const { hwnd, message, wParam, lParam, time } = messages[index];
            // Native x86 MSG has seven 32-bit words. The queue has no guest
            // cursor coordinates for a thread post. Preserve adjacent bytes.
            view.setUint32(at, hwnd, true);
            view.setUint32(at + 4, message, true);
            view.setUint32(at + 8, wParam, true);
            view.setInt32(at + 12, lParam, true);
            view.setUint32(at + 16, time, true);
            view.setInt32(at + 20, 0, true);
            view.setInt32(at + 24, 0, true);
            if ((flags & 1) !== 0) messages.splice(index, 1); // PM_REMOVE
            return 1;
        }
    };
}
