const disabledStyle = 0x08000000; // WS_DISABLED
const enableMessage = 0x000a; // WM_ENABLE
const cancelModeMessage = 0x001f; // WM_CANCELMODE

export const windowImportNames = ["isWindowEnabled", "enableWindow"];

export function createWindowState() {
    const windows = new Map();
    let nextHandle = 1;
    return {
        register(style, dispatch) {
            if (nextHandle > 0xffffffff) throw new Error("Win32 window handle space is exhausted.");
            const handle = nextHandle++;
            windows.set(handle, { style: style | 0, dispatch });
            return handle;
        },
        unregister(handle) {
            return windows.delete(handle >>> 0);
        },
        isWindowEnabled(handle) {
            const window = windows.get(handle >>> 0);
            return window && (window.style & disabledStyle) === 0 ? 1 : 0;
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
