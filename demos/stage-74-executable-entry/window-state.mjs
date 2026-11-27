import { getNonClientInsets } from "./window-rect.mjs";

const disabledStyle = 0x08000000; // WS_DISABLED
const childStyle = 0x40000000; // WS_CHILD
const setFocusMessage = 0x0007; // WM_SETFOCUS
const killFocusMessage = 0x0008; // WM_KILLFOCUS
const enableMessage = 0x000a; // WM_ENABLE
const cancelModeMessage = 0x001f; // WM_CANCELMODE

const invalidWindow = 1400, invalidIndex = 1413, invalidParameter = 87;
const procedureIndex = -4; // GWL_WNDPROC
let nextOwnerId = 1;
export const windowImportNames = [
    "isWindow", "isWindowEnabled", "enableWindow", "getWindowLongW", "getWindowLongPtrW", "setWindowLongW", "setWindowLongPtrW",
    "getActiveWindow", "setActiveWindow", "getFocus", "setFocus"
];

export function createWindowState() {
    const windows = new Map();
    let nextHandle = 1;
    let activeWindow = 0;
    let focusedWindow = 0;
    let focusChange = 0;
    let messageRoot = 0;
    let destroyedWindows = 0;
    if (nextOwnerId > 0xfffffffe) throw new RangeError("Win32 window owner ID space is exhausted.");
    // A launch currently owns one process and one GUI thread. These IDs
    // belong to the guest, independent of the renderer's OS identities.
    const owner = Object.freeze({ processId: nextOwnerId++, threadId: nextOwnerId++ });

    function changeFocus(handle, notifySame = false) {
        const previous = focusedWindow;
        if (previous === handle && !notifySame) return previous;
        const change = ++focusChange;
        focusedWindow = handle;
        // Both callbacks observe the new focus. A nested focus change or a
        // destruction in the first callback supersedes this notification.
        windows.get(previous)?.dispatch?.(killFocusMessage, handle, 0);
        if (focusChange === change) windows.get(handle)?.dispatch?.(setFocusMessage, previous, 0);
        return previous;
    }

    function activate(handle) {
        if (activeWindow === handle) return false;
        activeWindow = handle;
        changeFocus(handle);
        return true;
    }

    function topLevel(handle) {
        // Valid HWNDs form an ancestor chain ending at a non-child window.
        let enabled = true;
        for (let remaining = windows.size; remaining > 0; remaining--) {
            const window = windows.get(handle);
            if (!window) return null;
            enabled = enabled && (window.style & disabledStyle) === 0;
            if ((window.style & childStyle) === 0) return { handle, enabled };
            handle = window.attributes.parent >>> 0;
        }
        return null;
    }

    function register(style, dispatch, attributes = {}) {
        if (nextHandle > 0xffffffff) throw new Error("Win32 window handle space is exhausted.");
        const handle = nextHandle++;
        windows.set(handle, { style: style | 0, dispatch, title: "", attributes: { ...attributes },
            extra: new DataView(new ArrayBuffer(attributes.extraBytes ?? 0)) });
        return handle;
    }

    return {
        register,
        unregister(handle) {
            handle >>>= 0;
            if (handle === messageRoot) return false;
            const window = windows.get(handle);
            if (!window) return false;
            if (activeWindow === handle) {
                activeWindow = 0;
                focusedWindow = 0;
                focusChange++;
            } else if (focusedWindow === handle) {
                const parent = window.attributes.parent >>> 0;
                changeFocus(windows.has(parent) ? parent : 0);
            }
            if (windows.delete(handle)) destroyedWindows++;
            return true;
        },
        getActiveWindow() {
            return activeWindow;
        },
        setActiveWindow(handle, setError) {
            handle >>>= 0;
            if (handle === 0) return 0;
            const window = windows.get(handle);
            if (!window) { setError(invalidWindow); return 0; }
            const previous = activeWindow;
            // The launch models one guest input queue. Only a top-level HWND
            // becomes active, independently of the browser's DOM focus.
            if ((window.style & childStyle) === 0) activate(handle);
            return previous;
        },
        getFocus() {
            return focusedWindow;
        },
        setFocus(handle, setError) {
            handle >>>= 0;
            if (handle === 0) return changeFocus(0);
            const top = topLevel(handle);
            if (!top) { setError(invalidWindow); return 0; }
            if (!top.enabled) { setError(invalidParameter); return 0; }
            const activated = activate(top.handle);
            if (!windows.has(handle) || activeWindow !== top.handle) return 0;
            // Windows establishes the new top-level focus during activation,
            // then sends the caller's explicit focus change using that focus.
            return changeFocus(handle, activated && handle === top.handle);
        },
        hasWindow(handle) {
            return windows.has(handle >>> 0);
        },
        getDestroyedWindowCount() {
            return destroyedWindows;
        },
        isWindowChild(parent, handle) {
            for (let remaining = windows.size; remaining > 0; remaining--) {
                const child = windows.get(handle);
                if (!child || (child.style & childStyle) === 0) return false;
                handle = child.attributes.parent >>> 0;
                if (handle === parent) return true;
            }
            return false;
        },
        hasChildWindows(parent) {
            parent >>>= 0;
            for (const window of windows.values())
                if ((window.style & childStyle) !== 0 && (window.attributes.parent >>> 0) === parent) return true;
            return false;
        },
        getMessageRoot() {
            // Windows resolves HWND_MESSAGE to a real hidden parent before
            // dispatching CREATESTRUCT. Give each launch its own parent.
            if (messageRoot === 0) messageRoot = register(0);
            return messageRoot;
        },
        isMessageOnlyWindow(handle) {
            return windows.get(handle >>> 0)?.attributes.messageOnly === true;
        },
        hasFramedWindow(handle) {
            return windows.get(handle >>> 0)?.attributes.framed === true;
        },
        setWindowGeometry(handle, outer, client) {
            const window = windows.get(handle >>> 0);
            if (window) window.geometry = { outer, client };
        },
        getWindowGeometry(handle) {
            const geometry = windows.get(handle >>> 0)?.geometry;
            return geometry && { outer: geometry.outer.slice(), client: geometry.client.slice() };
        },
        getCurrentThreadId() {
            return owner.threadId;
        },
        getWindowOwner(handle) {
            return windows.has(handle >>> 0) ? owner : undefined;
        },
        getWindowClass(handle) {
            return windows.get(handle >>> 0)?.attributes.classRecord;
        },
        getWindowTitle(handle) {
            return windows.get(handle >>> 0)?.title;
        },
        setWindowTitle(handle, title) {
            const window = windows.get(handle >>> 0);
            if (window) window.title = title;
        },
        dispatchWindowMessage(handle, message, wparam, lparam) {
            return windows.get(handle >>> 0)?.dispatch?.(message, wparam, lparam);
        },
        isWindowEnabled(handle) {
            const window = windows.get(handle >>> 0);
            return window && (window.style & disabledStyle) === 0 ? 1 : 0;
        },
        getWindowLong(handle, index, setError) {
            const window = windows.get(handle >>> 0);
            if (!window) { setError(invalidWindow); return 0; }
            index |= 0;
            switch (index) {
                case procedureIndex: return window.attributes.procedure | 0;
                case -6: return window.attributes.instance | 0;
                case -8:
                    if (window.attributes.parent) return window.attributes.parent | 0;
                    break;
                case -12: return window.attributes.id | 0;
                case -16: return window.style;
                case -20: return window.attributes.extendedStyle | 0;
                case -21: return window.attributes.userData | 0;
                default:
                    if (index >= 0 && index <= window.extra.byteLength - 4) return window.extra.getInt32(index, true);
            }
            setError(invalidIndex);
            return 0;
        },
        setWindowProcedure(handle, index, procedure, bind, setError) {
            const window = windows.get(handle >>> 0);
            if (!window) { setError(invalidWindow); return 0; }
            if ((index | 0) !== procedureIndex) { setError(invalidIndex); return 0; }
            const dispatch = bind(procedure);
            if (!dispatch) { setError(invalidParameter); return 0; }
            const previous = window.attributes.procedure | 0;
            window.attributes.procedure = procedure;
            window.dispatch = dispatch;
            return previous;
        },
        enableWindow(handle, enable) {
            const key = handle >>> 0;
            const window = windows.get(key);
            if (!window) return 0;
            const previouslyDisabled = (window.style & disabledStyle) !== 0 ? 1 : 0;
            const enabled = enable !== 0;
            // Windows cancels the current mode before every disable request.
            if (!enabled) window.dispatch?.(cancelModeMessage, 0, 0);
            if (windows.get(key) !== window) return previouslyDisabled;
            const style = enabled ? window.style & ~disabledStyle : window.style | disabledStyle;
            if (style !== window.style) {
                window.style = style;
                if (!enabled && focusedWindow === key) changeFocus(0);
                if (windows.get(key) === window && window.style === style)
                    window.dispatch?.(enableMessage, enabled ? 1 : 0, 0);
            }
            return previouslyDisabled;
        }
    };
}

const classBytes = 40; // WNDCLASSW, x86
const nameOffset = 36, menuOffset = 32, moduleOffset = 16, procedureOffset = 4;
const classProcedureIndex = -24; // GCL_WNDPROC
const classAlreadyExists = 1410, classNotFound = 1411, noAccess = 998;
const notSupported = 50, notEnoughMemory = 8, cannotFindWindowClass = 1407;
const globalClass = 0x4000; // CS_GLOBALCLASS
const atomLimit = 0xffff;
const maximumNameUnits = 256;

// The Win32 x86 oracle folds ASCII class names and preserves other UTF-16 code units.
const classKey = name => name.replace(/[A-Z]/g, letter => letter.toLowerCase());

function readClassName(view, address, fail) {
    if (address <= atomLimit) return fail(invalidParameter);
    let name = "";
    for (let count = 0; count <= maximumNameUnits; count++) {
        const at = address + 2 * count;
        if (at > view.byteLength - 2) return fail(noAccess);
        const unit = view.getUint16(at, true);
        if (unit === 0) return name || fail(invalidParameter);
        if (count < maximumNameUnits) name += String.fromCharCode(unit);
    }
    return fail(invalidParameter);
}

export function createWindowClassState(memory, isProcedure, setError) {
    const atomsByName = new Map();
    const atomsByValue = new Map();
    const classFields = new WeakMap();
    let nextAtom = 0xc000; // string atoms use the upper quarter of the 16-bit atom space
    const fail = error => { setError(error); return 0; };
    function findClass(view, instance, identifier, missingError) {
        let entry;
        let name;
        if (identifier <= atomLimit) {
            entry = atomsByValue.get(identifier);
        } else {
            name = readClassName(view, identifier, fail);
            if (!name) return null;
            entry = atomsByName.get(classKey(name));
        }
        const record = entry?.modules.get(instance >>> 0);
        const fields = classFields.get(record);
        if (!fields) { fail(missingError); return null; }
        return { atom: entry.atom, name, fields, record };
    }

    return {
        style(record) {
            return classFields.get(record)?.[0] ?? 0;
        },
        findClass(instance, identifier) {
            const found = findClass(new DataView(memory.buffer), instance, identifier >>> 0, cannotFindWindowClass);
            if (!found) return null;
            return { name: found.name, procedure: found.fields[procedureOffset / 4],
                extraBytes: found.fields[3], record: found.record };
        },
        setClassProcedure(record, index, procedure) {
            const fields = classFields.get(record);
            if (!fields) return fail(invalidWindow);
            if ((index | 0) !== classProcedureIndex) return fail(invalidIndex);
            if (!isProcedure(procedure)) return fail(invalidParameter);
            // Windows snapshots its starting WNDPROC at creation. Update the
            // class record for future HWNDs and GetClassInfo, leaving live
            // dispatch functions attached to their individual windows.
            const slot = procedureOffset / 4;
            const previous = fields[slot];
            fields[slot] = procedure;
            return previous;
        },
        registerClassW(pointer) {
            const view = new DataView(memory.buffer);
            const address = pointer >>> 0;
            if (address === 0 || address > view.byteLength - classBytes) return fail(noAccess);
            const name = readClassName(view, view.getUint32(address + nameOffset, true), fail);
            if (!name) return 0;
            const module = view.getUint32(address + moduleOffset, true);
            const key = classKey(name);
            let entry = atomsByName.get(key);
            if (entry?.modules.has(module)) return fail(classAlreadyExists);
            const style = view.getUint32(address, true);
            const menu = view.getUint32(address + menuOffset, true);
            if ((style & globalClass) !== 0 || menu > atomLimit) return fail(notSupported);
            if (!isProcedure(view.getInt32(address + procedureOffset, true)) ||
                view.getInt32(address + 8, true) < 0 || view.getInt32(address + 12, true) < 0)
                return fail(invalidParameter);
            if (!entry) {
                if (nextAtom > atomLimit) return fail(notEnoughMemory);
                entry = { atom: nextAtom++, modules: new Map() };
                atomsByName.set(key, entry);
                atomsByValue.set(entry.atom, entry);
            }
            // Copy the fixed fields so no registration retains caller memory.
            const fields = new Int32Array(nameOffset / 4);
            for (let index = 0; index < fields.length; index++)
                fields[index] = view.getInt32(address + 4 * index, true);
            const record = {};
            classFields.set(record, fields);
            entry.modules.set(module, record);
            return entry.atom;
        },
        getClassInfoW(instance, className, output) {
            const view = new DataView(memory.buffer);
            const address = output >>> 0;
            if (address === 0 || address > view.byteLength - classBytes) return fail(noAccess);
            const identifier = className >>> 0;
            const found = findClass(view, instance, identifier, classNotFound);
            if (!found) return 0;
            const { fields } = found;
            for (let index = 0; index < fields.length; index++)
                view.setInt32(address + 4 * index, fields[index], true);
            // Windows returns the caller's LPCTSTR, which may be an atom.
            view.setUint32(address + nameOffset, identifier, true);
            return found.atom;
        }
    };
}

const popupStyle = 0x80000000, clipSiblingsStyle = 0x04000000, captionStyle = 0x00c00000;
const windowEdgeStyle = 0x00000100;
const supportedFramedStyles = 0x00cf0000 | clipSiblingsStyle | 0x02000000 | disabledStyle;
const supportedFramedExtendedStyles = windowEdgeStyle | 0x00010000 | 0x00040000; // WS_EX_CONTROLPARENT, WS_EX_APPWINDOW
const useDefault = -0x80000000;
const messageParent = -3; // HWND_MESSAGE
const createMessage = 0x0001, destroyMessage = 0x0002, moveMessage = 0x0003, sizeMessage = 0x0005;
const getMinMaxInfoMessage = 0x0024, ncCreateMessage = 0x0081, ncDestroyMessage = 0x0082, ncCalcSizeMessage = 0x0083;
const uahDestroyMessage = 0x0090; // Observed in the Windows x86 themed-window abort path.
const pageBytes = 65536;
const createStructBytes = 48, minMaxInfoBytes = 40, rectBytes = 16;
const maximumCaptionUnits = 32767;
const emptyWindowEdges = Object.freeze([0, 0, 0, 0]);

const visibleOrChildStyle = 0x50000000; // WS_VISIBLE | WS_CHILD
const hiddenRedrawFlags = 0x0001 | 0x0004 | 0x0008 | 0x0080 | 0x0100 | 0x0200 | 0x0400;

export function createWindowRectQueries(memory, windows, setError) {
    function query(hwnd, pointer, client) {
        if (!windows.hasWindow(hwnd)) { setError(invalidWindow); return 0; }
        const geometry = windows.getWindowGeometry(hwnd);
        if (!geometry) { setError(notSupported); return 0; }
        const address = pointer >>> 0;
        if (address === 0 || address > memory.buffer.byteLength - rectBytes) { setError(noAccess); return 0; }
        if (client) {
            const [left, top, right, bottom] = geometry.client;
            writeInts(memory, address, [0, 0, Math.max(0, right - left), Math.max(0, bottom - top)]);
        } else writeInts(memory, address, geometry.outer);
        return 1;
    }

    return {
        getWindowRect: (hwnd, pointer) => query(hwnd, pointer, false),
        getClientRect: (hwnd, pointer) => query(hwnd, pointer, true)
    };
}

export function createRedrawWindow(memory, windows, setError) {
    return function redrawWindow(hwnd, rectangle, region, flags) {
        // A null HWND requests desktop-wide invalidation on Windows.
        let error;
        if (hwnd === 0) error = notSupported;
        else if (!windows.hasWindow(hwnd)) error = invalidWindow;
        else if (region !== 0 || (flags & ~hiddenRedrawFlags) !== 0 ||
            (windows.getWindowLong(hwnd, -16, setError) & visibleOrChildStyle) !== 0)
            error = notSupported;
        else if (rectangle !== 0 && (rectangle >>> 0) > memory.buffer.byteLength - rectBytes)
            error = noAccess;
        else
            // Windows leaves a hidden top-level window clean even for the
            // synchronous timing flags. A successful raw call preserves last error.
            return 1;
        setError(error);
        return 0;
    };
}

export function createDefaultWindowProcedure(memory, windows) {
    return function defWindowProcW(hwnd, message, wparam, lparam) {
        if (message === 0) return 0; // WM_NULL
        if (!windows.hasWindow(hwnd)) {
            if (message === sizeMessage || message === moveMessage) return 0;
            throw new Error("StandardWasm: default window procedure requires a live HWND.");
        }
        const style = windows.getWindowLong(hwnd, -16, () => {});
        const extendedStyle = windows.getWindowLong(hwnd, -20, () => {});
        const framed = windows.isMessageOnlyWindow(hwnd) || windows.hasFramedWindow(hwnd);
        if (!framed && (((style & ~clipSiblingsStyle) >>> 0) !== popupStyle || extendedStyle !== 0))
            throw new Error("StandardWasm: default window procedure currently supports hidden borderless popups.");

        // The hidden-popup defaults leave their incoming structures unchanged.
        // WM_NCCREATE copies the text before the creation lease is released.
        if (message === sizeMessage || message === moveMessage) return 0;
        if (framed && (message === getMinMaxInfoMessage || message === destroyMessage || message === ncDestroyMessage ||
            message === setFocusMessage || message === killFocusMessage || message === enableMessage)) return 0;
        let size;
        if (message === ncCreateMessage || message === createMessage) size = createStructBytes;
        else if (message === ncCalcSizeMessage && wparam === 0) size = rectBytes;
        else throw new Error("StandardWasm: unsupported default window message 0x" + message.toString(16) + ".");
        const address = lparam >>> 0;
        if (address === 0 || address > memory.buffer.byteLength - size)
            throw new Error("StandardWasm: invalid guest buffer for the default window procedure.");
        const view = new DataView(memory.buffer);
        if (framed && message === ncCalcSizeMessage) {
            const { outer, top } = getNonClientInsets(style, extendedStyle, false, 96);
            const left = view.getInt32(address, true) + outer;
            const topEdge = view.getInt32(address + 4, true) + top;
            writeInts(memory, address, [left, topEdge, Math.max(left, view.getInt32(address + 8, true) - outer),
                Math.max(topEdge, view.getInt32(address + 12, true) - outer)]);
        }
        if (message !== ncCreateMessage) return 0;
        const caption = view.getUint32(address + 36, true);
        if (captionError(view, caption) !== 0)
            throw new Error("StandardWasm: invalid creation caption for the default window procedure.");
        let title = "";
        if (caption) {
            for (let offset = caption, unit; (unit = view.getUint16(offset, true)) !== 0; offset += 2)
                title += String.fromCharCode(unit);
        }
        windows.setWindowTitle(hwnd, title);
        return 1;
    };
}

function captionError(view, address) {
    if (address === 0) return 0;
    if (address <= atomLimit) return noAccess;
    for (let index = 0; index <= maximumCaptionUnits; index++) {
        const at = address + 2 * index;
        if (at > view.byteLength - 2) return noAccess;
        if (view.getUint16(at, true) === 0) return 0;
    }
    return invalidParameter;
}

function writeInts(memory, address, values) {
    const view = new DataView(memory.buffer);
    for (let index = 0; index < values.length; index++)
        view.setInt32(address + 4 * index, values[index], true);
}

const packPair = (low, high) => (low & 0xffff) | (high << 16);
const clampTrackSize = (requested, min, max) => Math.max(min, Math.min(requested, max));

function virtualWindowMetrics() {
    const width = Math.max(136, globalThis.screen?.availWidth || 1024);
    const height = Math.max(39, globalThis.screen?.availHeight || 768);
    const { outer, top } = getNonClientInsets(captionStyle, windowEdgeStyle, false, 96);
    return [0, 0, width + 2 * outer, height + outer + top, -outer, -outer,
        136, outer + top, width + 2 * outer, height + 2 * outer];
}

export function createWindowCreation(memory, windows, classes, bindProcedure, setError, minMaxInfo) {
    const trackDefaults = minMaxInfo === undefined ? null : Array.from(minMaxInfo);
    if (trackDefaults && (trackDefaults.length !== 10 || !trackDefaults.every(value => (value | 0) === value) ||
        trackDefaults[6] < 0 || trackDefaults[7] < 0 ||
        trackDefaults[6] > trackDefaults[8] || trackDefaults[7] > trackDefaults[9]))
        throw new TypeError("Invalid Win32 MINMAXINFO defaults.");

    // Each active creation owns a page, reused only after that call returns.
    // Appending pages keeps this native scratch disjoint from the guest allocator.
    const frames = [];
    let depth = 0;
    let nextCascadeOffset = 0;
    const fail = error => { setError(error); return 0; };

    function defaultWindowOffset(width, height, metrics) {
        // The host owns placement on its virtual display. Explicit positioning
        // and message-only windows do not advance the overlapped cascade.
        const step = Math.max(1, metrics[7] + metrics[4] + metrics[5]);
        const limit = Math.max(0, Math.min(metrics[8] - width, metrics[9] - height));
        if (nextCascadeOffset > limit) nextCascadeOffset = 0;
        const offset = nextCascadeOffset;
        nextCascadeOffset += step;
        return offset;
    }

    return function createWindowExW(exStyle, className, caption, style, x, y, width, height, parent, menu, instance, param) {
        const windowClass = classes.findClass(instance, className);
        if (!windowClass) return 0;
        const messageOnly = parent === messageParent;
        const popup = (style >>> 0) === popupStyle;
        const framed = parent === 0 && (style & captionStyle) === captionStyle && (style & ~supportedFramedStyles) === 0;
        const supportedStyle = messageOnly
            ? (style & (visibleOrChildStyle | popupStyle)) === 0
            : parent === 0 && (framed || popup || style === 0 && trackDefaults !== null);
        const extendedStyleMask = framed ? supportedFramedExtendedStyles : 0;
        const defaultPosition = x === useDefault;
        if ((exStyle & ~extendedStyleMask) !== 0 || menu !== 0 || !supportedStyle ||
            (defaultPosition && !framed) || (!defaultPosition && y === useDefault) ||
            width === useDefault || height === useDefault)
            return fail(notSupported);
        if (width < 0 || height < 0) return fail(invalidParameter);
        const captionStatus = captionError(new DataView(memory.buffer), caption >>> 0);
        if (captionStatus !== 0) return fail(captionStatus);
        const dispatch = bindProcedure(windowClass.procedure);
        if (!dispatch) return fail(invalidParameter);

        let frame, handle = 0, committed = false;
        try {
            frame = frames[depth] ??= memory.grow(1) * pageBytes;
        } catch (error) {
            if (!(error instanceof RangeError)) throw error;
            return fail(notEnoughMemory);
        }
        depth++;
        try {
            const liveStyle = style | clipSiblingsStyle | (popup ? 0 : captionStyle);
            const liveExStyle = popup ? exStyle : exStyle | windowEdgeStyle;
            let actualParent;
            try {
                actualParent = messageOnly ? windows.getMessageRoot() : 0;
                handle = windows.register(liveStyle, (message, wparam, lparam) => dispatch(handle, message, wparam, lparam), {
                    procedure: windowClass.procedure, instance, extendedStyle: liveExStyle, parent: actualParent, messageOnly, framed,
                    extraBytes: windowClass.extraBytes, classRecord: windowClass.record
                });
            } catch (error) {
                if (!(error instanceof RangeError)) throw error;
                return fail(notEnoughMemory);
            }
            const send = (message, lparam = 0) => windows.dispatchWindowMessage(handle, message, 0, lparam | 0);
            const track = frame + createStructBytes;
            const rect = track + minMaxInfoBytes;
            const classCopy = rect + rectBytes;
            const classPointer = windowClass.name === undefined ? className : classCopy;
            if (windowClass.name !== undefined) {
                const view = new DataView(memory.buffer);
                for (let index = 0; index < windowClass.name.length; index++)
                    view.setUint16(classCopy + 2 * index, windowClass.name.charCodeAt(index), true);
                view.setUint16(classCopy + 2 * windowClass.name.length, 0, true);
            }
            let outerWidth = width, outerHeight = height;
            // Win32 publishes each side of the geometry before its next callback.
            // Both are empty in WM_GETMINMAXINFO. The client is empty until WM_NCCALCSIZE returns.
            windows.setWindowGeometry(handle, emptyWindowEdges, emptyWindowEdges);
            if (!popup) {
                const metrics = trackDefaults ?? virtualWindowMetrics();
                if (defaultPosition) x = y = defaultWindowOffset(width, height, metrics);
                writeInts(memory, track, metrics);
                send(getMinMaxInfoMessage, track);
                if (!windows.hasWindow(handle)) return 0;
                // A callback can grow memory or re-enter CreateWindowExW.
                const view = new DataView(memory.buffer);
                outerWidth = clampTrackSize(width, view.getInt32(track + 24, true), view.getInt32(track + 32, true));
                outerHeight = clampTrackSize(height, view.getInt32(track + 28, true), view.getInt32(track + 36, true));
            }
            const outer = [x, y, x + outerWidth, y + outerHeight];
            windows.setWindowGeometry(handle, outer, emptyWindowEdges);
            const parameters = [param, instance, menu, actualParent, height, width, y, x, style, caption, classPointer,
                framed ? liveExStyle : exStyle];
            writeInts(memory, frame, parameters);
            if (!send(ncCreateMessage, frame)) {
                send(ncDestroyMessage);
                return 0;
            }
            if (!windows.hasWindow(handle)) return 0;
            writeInts(memory, rect, outer);
            send(ncCalcSizeMessage, rect);
            if (!windows.hasWindow(handle)) return 0;
            const view = new DataView(memory.buffer);
            const client = Array.from({ length: 4 }, (_, index) => view.getInt32(rect + index * 4, true));
            windows.setWindowGeometry(handle, outer, client);
            // Win32 rematerializes the original request at the same CREATESTRUCT address.
            writeInts(memory, frame, parameters);
            if (send(createMessage, frame) === -1) {
                send(uahDestroyMessage);
                send(destroyMessage);
                send(ncDestroyMessage);
                return 0;
            }
            if (popup && windows.hasWindow(handle)) {
                const [left, top, right, bottom] = client;
                send(sizeMessage, packPair(Math.max(0, right - left), Math.max(0, bottom - top)));
                send(moveMessage, packPair(left, top));
            }
            committed = windows.hasWindow(handle);
            return committed ? handle : 0;
        } finally {
            depth--;
            if (handle !== 0 && !committed) windows.unregister(handle);
        }
    };
}
