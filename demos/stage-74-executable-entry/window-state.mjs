const disabledStyle = 0x08000000; // WS_DISABLED
const enableMessage = 0x000a; // WM_ENABLE
const cancelModeMessage = 0x001f; // WM_CANCELMODE

const invalidWindow = 1400, invalidIndex = 1413, invalidParameter = 87;
const procedureIndex = -4; // GWL_WNDPROC
export const windowImportNames = [
    "isWindowEnabled", "enableWindow", "getWindowLongW", "getWindowLongPtrW", "setWindowLongW", "setWindowLongPtrW"
];

export function createWindowState() {
    const windows = new Map();
    let nextHandle = 1;
    return {
        register(style, dispatch, attributes = {}) {
            if (nextHandle > 0xffffffff) throw new Error("Win32 window handle space is exhausted.");
            const handle = nextHandle++;
            windows.set(handle, { style: style | 0, dispatch, attributes: { ...attributes },
                extra: new DataView(new ArrayBuffer(attributes.extraBytes ?? 0)) });
            return handle;
        },
        unregister(handle) {
            return windows.delete(handle >>> 0);
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
                window.dispatch?.(enableMessage, enabled ? 1 : 0, 0);
            }
            return previouslyDisabled;
        }
    };
}

const classBytes = 40; // WNDCLASSW, x86
const nameOffset = 36, menuOffset = 32, moduleOffset = 16, procedureOffset = 4;
const classAlreadyExists = 1410, classNotFound = 1411, noAccess = 998;
const notSupported = 50, noAtoms = 8;
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
    let nextAtom = 0xc000; // string atoms use the upper quarter of the 16-bit atom space
    const fail = error => { setError(error); return 0; };

    return {
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
                if (nextAtom > atomLimit) return fail(noAtoms);
                entry = { atom: nextAtom++, modules: new Map() };
                atomsByName.set(key, entry);
                atomsByValue.set(entry.atom, entry);
            }
            // Copy the fixed fields so no registration retains caller memory.
            const fields = new Int32Array(nameOffset / 4);
            for (let index = 0; index < fields.length; index++)
                fields[index] = view.getInt32(address + 4 * index, true);
            entry.modules.set(module, fields);
            return entry.atom;
        },
        getClassInfoW(instance, className, output) {
            const view = new DataView(memory.buffer);
            const address = output >>> 0;
            if (address === 0 || address > view.byteLength - classBytes) return fail(noAccess);
            const identifier = className >>> 0;
            let entry;
            if (identifier <= atomLimit) {
                entry = atomsByValue.get(identifier);
            } else {
                const name = readClassName(view, identifier, fail);
                if (!name) return 0;
                entry = atomsByName.get(classKey(name));
            }
            const fields = entry?.modules.get(instance >>> 0);
            if (!fields) return fail(classNotFound);
            for (let index = 0; index < fields.length; index++)
                view.setInt32(address + 4 * index, fields[index], true);
            // Windows returns the caller's LPCTSTR, which may be an atom.
            view.setUint32(address + nameOffset, identifier, true);
            return entry.atom;
        }
    };
}
