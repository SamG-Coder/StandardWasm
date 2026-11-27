export const displayContextImportNames = ["getDC", "releaseDC", "getDeviceCaps"];

const bitsPixel = 12, planes = 14, logPixelsX = 88, logPixelsY = 90;
let nextContext = 0x40000000;

export function browserDisplayCapabilities() {
    const ratio = globalThis.devicePixelRatio;
    const colorDepth = globalThis.screen?.colorDepth;
    // The guest sees a single logical display. CSS pixels have a 96-DPI
    // reference, and the browser exposes the scale and color depth separately.
    if (!Number.isFinite(ratio) || ratio <= 0 || !Number.isInteger(colorDepth) || colorDepth <= 0)
        return undefined;
    const dpi = Math.max(1, Math.round(96 * ratio));
    return { [bitsPixel]: colorDepth, [planes]: 1, [logPixelsX]: dpi, [logPixelsY]: dpi };
}

export function createDisplayContexts(windows, windowClassStyle, capabilities) {
    const contexts = new Map();
    return {
        getDC(hwnd) {
            hwnd >>>= 0;
            if (hwnd !== 0 && !windows.hasWindow(hwnd)) return 0;
            // CS_OWNDC and CS_CLASSDC require persistent GDI state.
            if ((windowClassStyle(hwnd) & 0x60) !== 0)
                throw Error("StandardWasm GetDC does not support private or class device contexts.");
            if (nextContext > 0x7fffffff) throw new RangeError("Win32 display context handles are exhausted.");
            const handle = nextContext++;
            contexts.set(handle, hwnd);
            return handle;
        },
        releaseDC(hwnd, handle) {
            if (contexts.get(handle >>> 0) !== (hwnd >>> 0)) return 0;
            contexts.delete(handle >>> 0);
            return 1;
        },
        getDeviceCaps(handle, selector) {
            if (!contexts.has(handle >>> 0)) return 0;
            const profile = capabilities ?? browserDisplayCapabilities();
            const value = profile?.[selector | 0];
            if (!Number.isInteger(value) || value < -0x80000000 || value > 0x7fffffff)
                throw Error(`StandardWasm GetDeviceCaps has no display capability for selector ${selector | 0}.`);
            return value;
        }
    };
}
