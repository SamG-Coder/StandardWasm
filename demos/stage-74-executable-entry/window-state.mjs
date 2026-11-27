const disabledStyle = 0x08000000; // WS_DISABLED
const enableMessage = 0x000a; // WM_ENABLE
const cancelModeMessage = 0x001f; // WM_CANCELMODE

const invalidWindow = 1400, invalidIndex = 1413;
export const windowImportNames = ["isWindowEnabled", "enableWindow", "getWindowLongW", "getWindowLongPtrW"];

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
                case -4: return window.attributes.procedure | 0;
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
