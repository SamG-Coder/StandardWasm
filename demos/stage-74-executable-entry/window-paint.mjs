// Win32 update regions and paint delivery for the launch's guest windows.
//
// Windows keeps one update region per window. It is the union of invalidated
// client rectangles, minus validated ones. WM_PAINT is not a posted message:
// PeekMessageW synthesizes it after no posted message is eligible, and it
// stays retrievable until BeginPaint or ValidateRect empties the region.
// The region is stored exactly, as disjoint rectangles. GetUpdateRect and
// rcPaint report its bounding box, as on Windows, but partial validation
// needs the real shape.
//
// Measured on Windows x86 (Stage 340): InvalidateRect and ValidateRect both
// normalize inverted rectangles. An erase request is kept only when the
// clipped invalid area is non-empty, and emptying the region cancels it.
// PAINTSTRUCT.fErase stays set after a WM_ERASEBKGND that returned zero. It
// is cleared only by a later WM_ERASEBKGND that returns nonzero.

export const paintImportNames = ["beginPaint", "endPaint", "getUpdateRect", "validateRect"];

const paintMessage = 0x000f; // WM_PAINT
const eraseBackgroundMessage = 0x0014; // WM_ERASEBKGND
const visibleStyle = 0x10000000; // WS_VISIBLE
const childStyle = 0x40000000; // WS_CHILD
const queuePaint = 0x20; // QS_PAINT in the high word of PeekMessage flags
const invalidWindow = 1400, noAccess = 998, notSupported = 50;
const rectBytes = 16, paintStructBytes = 64; // x86 RECT and PAINTSTRUCT

const isEmpty = ([left, top, right, bottom]) => right <= left || bottom <= top;

function intersect(a, b) {
    return [Math.max(a[0], b[0]), Math.max(a[1], b[1]), Math.min(a[2], b[2]), Math.min(a[3], b[3])];
}

// Returns the parts of `rect` outside `cut`. There are at most four of them
// and they do not overlap: the bands above and below, then left and right.
function subtract(rect, cut) {
    const overlap = intersect(rect, cut);
    if (isEmpty(overlap)) return [rect];
    const [left, top, right, bottom] = rect;
    const pieces = [
        [left, top, right, overlap[1]],
        [left, overlap[3], right, bottom],
        [left, overlap[1], overlap[0], overlap[3]],
        [overlap[2], overlap[1], right, overlap[3]]
    ];
    return pieces.filter(piece => !isEmpty(piece));
}

function boundingBox(region) {
    if (region.length === 0) return [0, 0, 0, 0];
    return region.reduce((box, rect) => [Math.min(box[0], rect[0]), Math.min(box[1], rect[1]),
        Math.max(box[2], rect[2]), Math.max(box[3], rect[3])]);
}

export function createWindowPaint(memory, windows, contexts, setError) {
    // Paint state lives beside the window table. HWND values are never reused
    // within a launch, so state for a destroyed handle is never consulted again.
    // It is dropped when the queue observes the destruction.
    const states = new Map();
    let seenDestructions = windows.getDestroyedWindowCount();

    function fail(error) {
        setError(error);
        return 0;
    }

    function stateOf(hwnd) {
        const count = windows.getDestroyedWindowCount();
        if (count !== seenDestructions) {
            seenDestructions = count;
            for (const handle of states.keys()) if (!windows.hasWindow(handle)) states.delete(handle);
        }
        let state = states.get(hwnd);
        if (!state) states.set(hwnd, state = { region: [], sendErase: false, eraseUnhandled: false });
        return state;
    }

    function style(hwnd) {
        return windows.getWindowLong(hwnd, -16, () => {});
    }

    function clientBounds(hwnd) {
        const geometry = windows.getWindowGeometry(hwnd);
        if (!geometry) return null;
        const [left, top, right, bottom] = geometry.client;
        return [0, 0, Math.max(0, right - left), Math.max(0, bottom - top)];
    }

    function inBounds(pointer, size) {
        const address = pointer >>> 0;
        return address !== 0 && address <= memory.buffer.byteLength - size;
    }

    // Reads a caller RECT and orders its edges as Windows does.
    function readRect(pointer) {
        const view = new DataView(memory.buffer);
        const at = pointer >>> 0;
        const [left, top, right, bottom] = [0, 4, 8, 12].map(offset => view.getInt32(at + offset, true));
        return [Math.min(left, right), Math.min(top, bottom), Math.max(left, right), Math.max(top, bottom)];
    }

    function writeWords(address, values) {
        const view = new DataView(memory.buffer);
        values.forEach((value, index) => view.setInt32(address + index * 4, value, true));
    }

    // Sends WM_ERASEBKGND when an erase is pending. A zero result leaves the
    // background for the application, which BeginPaint reports through fErase.
    function erase(hwnd, state, context) {
        if (!state.sendErase) return;
        state.sendErase = false;
        const result = windows.dispatchWindowMessage(hwnd, eraseBackgroundMessage, context, 0);
        state.eraseUnhandled = (result | 0) === 0;
    }

    function hasChildren(hwnd) {
        return windows.hasChildWindows(hwnd);
    }

    function invalidateRect(hwnd, rectangle, eraseRequest) {
        hwnd >>>= 0;
        // A null HWND invalidates every desktop window on Windows.
        if (hwnd === 0) return fail(notSupported);
        if (!windows.hasWindow(hwnd)) {
            // Windows x86 reports ERROR_INVALID_WINDOW_HANDLE but returns
            // TRUE for a stale or otherwise invalid non-null HWND.
            setError(invalidWindow);
            return 1;
        }
        const windowStyle = style(hwnd);
        // Child windows and parents of children need WS_CLIPCHILDREN and
        // sibling propagation, which the host does not model yet.
        if ((windowStyle & childStyle) !== 0 || hasChildren(hwnd)) return fail(notSupported);
        if (rectangle !== 0 && !inBounds(rectangle, rectBytes)) return fail(noAccess);
        // A hidden top-level client stays clean for either erase value.
        // The RECT is borrowed read-only.
        if ((windowStyle & visibleStyle) === 0) return 1;
        const client = clientBounds(hwnd);
        if (!client) return fail(notSupported);
        const added = intersect(rectangle === 0 ? client : readRect(rectangle), client);
        if (isEmpty(added)) return 1;
        const state = stateOf(hwnd);
        let pieces = [added];
        for (const existing of state.region) pieces = pieces.flatMap(piece => subtract(piece, existing));
        state.region.push(...pieces);
        if (eraseRequest !== 0) state.sendErase = true;
        return 1;
    }

    function validateRect(hwnd, rectangle) {
        hwnd >>>= 0;
        // A null HWND makes Windows repaint every window.
        if (hwnd === 0) return fail(notSupported);
        if (!windows.hasWindow(hwnd)) return fail(invalidWindow);
        if (rectangle !== 0 && !inBounds(rectangle, rectBytes)) return fail(noAccess);
        const state = stateOf(hwnd);
        if (rectangle === 0) state.region = [];
        else {
            const cut = readRect(rectangle);
            state.region = state.region.flatMap(rect => subtract(rect, cut));
        }
        if (state.region.length === 0) state.sendErase = false;
        return 1;
    }

    function getUpdateRect(hwnd, rectangle, eraseRequest) {
        hwnd >>>= 0;
        if (!windows.hasWindow(hwnd)) return fail(invalidWindow);
        if (rectangle !== 0 && !inBounds(rectangle, rectBytes)) return fail(noAccess);
        const state = stateOf(hwnd);
        const dirty = state.region.length !== 0;
        if (dirty && eraseRequest !== 0 && state.sendErase) {
            // Windows erases through its own temporary DC, not a paint DC.
            const context = contexts.getDC(hwnd);
            try {
                erase(hwnd, state, context);
            } finally {
                contexts.releaseDC(hwnd, context);
            }
        }
        // A callback may have changed the region or grown memory.
        if (rectangle !== 0) writeWords(rectangle >>> 0, boundingBox(stateOf(hwnd).region));
        return stateOf(hwnd).region.length !== 0 ? 1 : 0;
    }

    function beginPaint(hwnd, paint) {
        hwnd >>>= 0;
        if (!windows.hasWindow(hwnd)) return fail(invalidWindow);
        if (!inBounds(paint, paintStructBytes)) return fail(noAccess);
        const state = stateOf(hwnd);
        const context = contexts.getDC(hwnd);
        // BeginPaint takes the update region before it erases, so it validates
        // the window even when the erase callback queries the region.
        const area = boundingBox(state.region);
        state.region = [];
        erase(hwnd, state, context);
        const at = paint >>> 0;
        writeWords(at, [context, state.eraseUnhandled ? 1 : 0, ...area, 0, 0]);
        new Uint8Array(memory.buffer, at + 32, 32).fill(0); // rgbReserved
        return context;
    }

    function endPaint(hwnd, paint) {
        hwnd >>>= 0;
        if (!windows.hasWindow(hwnd)) return fail(invalidWindow);
        if (!inBounds(paint, paintStructBytes)) return fail(noAccess);
        const context = new DataView(memory.buffer).getUint32(paint >>> 0, true);
        // A context that does not belong to this window is ignored, as with ReleaseDC.
        contexts.releaseDC(hwnd, context);
        return 1;
    }

    // Returns the HWND for a synthesized WM_PAINT, or 0. PeekMessageW calls
    // this only after its posted-message search finds nothing.
    function nextPaint(filter, first, last, queueStatus) {
        if (queueStatus !== 0 && (queueStatus & queuePaint) === 0) return 0;
        const allMessages = first === 0 && last === 0;
        if (!allMessages && (paintMessage < first || paintMessage > last)) return 0;
        if (filter === 0xffffffff) return 0; // thread messages only
        const dirty = [];
        for (const [handle, state] of states)
            if (state.region.length !== 0 && windows.hasWindow(handle) &&
                (filter === 0 || handle === filter || windows.isWindowChild(filter, handle))) dirty.push(handle);
        if (dirty.length === 0) return 0;
        // Windows delivers paint in z-order. The host's z-order is creation
        // order, newest on top, until activation reorders windows.
        if (dirty.length > 1 && windows.getActiveWindow() !== 0)
            throw new Error("StandardWasm: paint order among several dirty windows after activation requires z-order tracking.");
        return Math.max(...dirty);
    }

    return {
        imports: { invalidateRect, validateRect, getUpdateRect, beginPaint, endPaint },
        nextPaint
    };
}
