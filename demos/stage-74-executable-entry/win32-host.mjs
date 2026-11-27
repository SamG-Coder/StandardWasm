import { readPeResource } from "../stage-73-executable-intake/loader/pe.mjs";
import { createActivationContexts } from "./activation-context.mjs";
import { createDpiAwareness, dpiImportNames } from "./dpi-awareness.mjs";
import { createWindowState, windowImportNames } from "./window-state.mjs";

export const win32ImportModule = "standardwasm:win32";
export const win32MemoryImports = new Set([
    "getModuleHandleW", "createActCtxW", "getCurrentActCtx", "activateActCtx"
]);
export const win32ImportNames = new Set([
    ...win32MemoryImports, ...dpiImportNames, ...windowImportNames, "deactivateActCtx", "releaseActCtx",
    "getLastSystemError", "setLastSystemError", "getLastPInvokeError", "setLastPInvokeError"
]);

const moduleNotFound = 126;
const noAccess = 998;
const executableHandle = 1;
const decoder = new TextDecoder("utf-16le", { ignoreBOM: true });
const normalize = name => name.replaceAll("\\", "/").toLowerCase();

export function createWin32Host(memory, executablePath, assets = [], windows = createWindowState()) {
    const byPath = new Map();
    const byBaseName = new Map();
    const moduleImages = new Map();
    const baseName = name => name.slice(name.lastIndexOf("/") + 1);
    function register(path, handle) {
        const name = normalize(path);
        if (!name || byPath.has(name)) return false;
        byPath.set(name, handle);
        const leaf = baseName(name);
        if (!byBaseName.has(leaf)) byBaseName.set(leaf, handle);
        return true;
    }
    if (executablePath) register(executablePath, executableHandle);
    let nextHandle = executableHandle + 1;
    // File-backed manifested DLLs can provide Win32 activation-context
    // resources. Images unpacked from a bundle keep the managed fallback.
    for (const asset of assets) {
        if (!asset.loadedFromBytes && readPeResource(asset.bytes, 24, 2, 0) &&
            register(asset.path, nextHandle)) moduleImages.set(nextHandle++, asset.bytes);
    }

    let systemError = 0;
    let pinvokeError = 0;

    function getModuleHandleW(pointer) {
        const address = pointer >>> 0;
        if (!address) return executableHandle;

        // A buffer view is refreshed after every memory.grow. Never read
        // beyond the explicit linear-memory instance supplied by the loader.
        const bytes = memory.buffer;
        const view = new DataView(bytes);
        let end = address;
        while (end + 1 < view.byteLength && view.getUint16(end, true) !== 0) end += 2;
        if (end + 1 >= view.byteLength) {
            systemError = noAccess;
            return 0;
        }

        let name = normalize(decoder.decode(new Uint8Array(bytes, address, end - address)));
        if (name) {
            const leaf = baseName(name);
            if (!leaf.includes(".")) name += ".dll";
            else if (leaf.endsWith(".")) name = name.slice(0, -1);
            const handle = (name.includes("/") ? byPath : byBaseName).get(name);
            if (handle !== undefined) return handle;
        }
        systemError = moduleNotFound;
        return 0;
    }

    function findManifest(handle, id) {
        const image = moduleImages.get(handle);
        return image && readPeResource(image, 24, id, 0);
    }

    return {
        getModuleHandleW,
        ...createActivationContexts(memory, findManifest, value => { systemError = value; }),
        ...createDpiAwareness(value => { systemError = value; }),
        isWindowEnabled: handle => windows.isWindowEnabled(handle),
        enableWindow: (handle, enable) => windows.enableWindow(handle, enable),
        getLastSystemError: () => systemError,
        setLastSystemError: value => { systemError = value | 0; },
        getLastPInvokeError: () => pinvokeError,
        setLastPInvokeError: value => { pinvokeError = value | 0; }
    };
}
