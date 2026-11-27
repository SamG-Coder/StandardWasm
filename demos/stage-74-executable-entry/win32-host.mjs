import { readPeResource } from "../stage-73-executable-intake/loader/pe.mjs";
import { createActivationContexts } from "./activation-context.mjs";
import { createDpiAwareness, dpiImportNames } from "./dpi-awareness.mjs";
import { createDisplayContexts, displayContextImportNames } from "./display-context.mjs";
import { createFullPathHost } from "./full-path-host.mjs";
import { createFilePathHost } from "./long-path-host.mjs";
import { createAdjustWindowRectExForDpi } from "./window-rect.mjs";
import { createThreadMessageQueue, createWindowMessageRegistration } from "./window-messages.mjs";
import { createWindowEventPublisher } from "./window-events.mjs";
import { createWindowSender } from "./window-send.mjs";
import { createWindowPaint, paintImportNames } from "./window-paint.mjs";
import { createThreadHandleHost } from "./thread-handle-host.mjs";
import { createWindowStationHost } from "./window-station-host.mjs";
import { createDefaultWindowProcedure, createWindowClassState, createWindowCreation, createRedrawWindow,
    createWindowRectQueries, createWindowState, windowImportNames } from "./window-state.mjs";

export const win32ImportModule = "standardwasm:win32";
export const win32MemoryImports = new Set([
    "getModuleHandleW", "loadLibraryExW", "getProcAddress", "createActCtxW", "getCurrentActCtx", "activateActCtx", "bCryptGenRandom",
    "registerClassW", "getClassInfoW", "createWindowExW", "getFullPathNameW", "getLongPathNameW", "redrawWindow", "invalidateRect", "registerWindowMessageW",
    "adjustWindowRectExForDpi", "getWindowThreadProcessId", "duplicateHandle", "getExitCodeThread", "getUserObjectInformationW",
    "peekMessageW", "getWindowRect", "getClientRect", "deleteFileW", "sendMessageTimeoutW", ...paintImportNames
]);
export const win32ImportNames = new Set([
    ...win32MemoryImports, ...dpiImportNames, ...windowImportNames, ...displayContextImportNames,
    "callWindowProcW", "setClassLongW", "deactivateActCtx", "releaseActCtx",
    "getCurrentProcess", "getCurrentThread", "getCurrentThreadId", "closeHandle", "getProcessWindowStation",
    "getLastSystemError", "setLastSystemError", "getLastPInvokeError", "setLastPInvokeError", "postThreadMessageW", "postMessageW",
    "notifyWinEvent"
]);

const moduleNotFound = 126;
const noAccess = 998;
const invalidWindowHandle = 1400;
const invalidWin32Parameter = 87;
const invalidParameter = 0xc000000d | 0, invalidHandle = 0xc0000008 | 0, unsuccessful = 0xc0000001 | 0;
const executableHandle = 1;
const windowProcedureSignature = "stdcall:i32(p32,u32,u32,i32)";
const decoder = new TextDecoder("utf-16le", { ignoreBOM: true });
const normalize = name => name.replaceAll("\\", "/").toLowerCase();

// Reader for the native UTF-16 buffer, shared by both library APIs.
function readWideString(memory, address) {
    const bytes = memory.buffer;
    const view = new DataView(bytes);
    let end = address;
    while (end + 1 < view.byteLength && view.getUint16(end, true) !== 0) end += 2;
    if (end + 1 >= view.byteLength) return null;
    return decoder.decode(new Uint8Array(bytes, address, end - address));
}

export function createNativeSymbolLookup(memory, libraries, errors) {
    return function getNativeSymbol(_state, handle, pointer) {
        const address = pointer >>> 0;
        const name = address === 0 ? null : readWideString(memory, address);
        if (name === null) {
            errors.system = noAccess;
            return 0;
        }
        // The generated thunk applies the original QCall's throwOnError flag.
        return libraries.getProcAddress(handle, name);
    };
}

export function createWin32Host(memory, executablePath, assets = [], windows = createWindowState(),
    errors = { system: 0, pinvoke: 0 }, dynamicLibraries, windowCreationMetrics, win32FileSystem, desktop = false,
    displayCapabilities, onWinEvent) {
    if (desktop && dynamicLibraries && !dynamicLibraries.hasLibrary("user32.dll")) {
        dynamicLibraries.registerLibrary("user32.dll", new Map([["DefWindowProcW", {
            signature: windowProcedureSignature,
            invoke: createDefaultWindowProcedure(memory, windows)
        }]]), true);
    }
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

    const setError = value => { errors.system = value; };
    function getWindowThreadProcessId(handle, processPointer) {
        const owner = windows.getWindowOwner(handle);
        if (!owner) {
            setError(invalidWindowHandle);
            return 0;
        }
        const address = processPointer >>> 0;
        if (address !== 0) {
            const view = new DataView(memory.buffer);
            if (address > view.byteLength - 4) {
                setError(noAccess);
                return 0;
            }
            view.setUint32(address, owner.processId, true);
        }
        return owner.threadId;
    }
    const isProcedure = token => dynamicLibraries?.hasTarget(token, windowProcedureSignature);
    const classes = createWindowClassState(memory, isProcedure, setError);
    const bindProcedure = token => isProcedure(token)
        ? (handle, message, wparam, lparam) => dynamicLibraries.invoke(token, windowProcedureSignature, handle, message, wparam, lparam)
        : undefined;
    const displayContexts = createDisplayContexts(windows, hwnd => classes.style(windows.getWindowClass(hwnd)), displayCapabilities);
    const paint = createWindowPaint(memory, windows, displayContexts, setError);
    const getWindowLong = (handle, index) => windows.getWindowLong(handle, index, setError);
    function setWindowLong(handle, index, token) {
        return windows.setWindowProcedure(handle, index, token, procedure => {
            const dispatch = bindProcedure(procedure);
            return dispatch && ((message, wparam, lparam) => dispatch(handle, message, wparam, lparam));
        }, setError);
    }

    function loadLibraryExW(pointer, reserved, flags) {
        const address = pointer >>> 0;
        if (!address) {
            errors.system = invalidWin32Parameter;
            return 0;
        }
        const name = readWideString(memory, address);
        if (name === null) {
            errors.system = noAccess;
            return 0;
        }
        return dynamicLibraries.loadLibrary(name, reserved, flags);
    }

    function getProcAddress(handle, pointer) {
        const address = pointer >>> 0;
        // The Win32 API treats values below 64 KiB as ordinals. The
        // registered browser providers currently advertise named exports.
        if (address <= 0xffff) return dynamicLibraries.getProcAddress(handle, address);
        const bytes = new Uint8Array(memory.buffer);
        const end = bytes.indexOf(0, address);
        if (end < 0) {
            errors.system = noAccess;
            return 0;
        }
        // Preserve the raw PCSTR bytes. In particular, export lookup is
        // case-sensitive and does not normalize or decode a UTF-8 name.
        let name = "";
        for (let at = address; at < end; at++) name += String.fromCharCode(bytes[at]);
        return dynamicLibraries.getProcAddress(handle, name);
    }

    function getModuleHandleW(pointer) {
        const address = pointer >>> 0;
        if (!address) return executableHandle;
        const text = readWideString(memory, address);
        if (text === null) {
            errors.system = noAccess;
            return 0;
        }
        const requested = normalize(text);
        let name = requested;
        if (name) {
            const leaf = baseName(name);
            if (!leaf.includes(".")) name += ".dll";
            else if (leaf.endsWith(".")) name = name.slice(0, -1);
            const handle = (name.includes("/") ? byPath : byBaseName).get(name);
            if (handle !== undefined) return handle;
        }
        if (dynamicLibraries) return dynamicLibraries.getModuleHandle(requested);
        errors.system = moduleNotFound;
        return 0;
    }

    function findManifest(handle, id) {
        const image = moduleImages.get(handle);
        return image && readPeResource(image, 24, id, 0);
    }

    function bCryptGenRandom(algorithm, pointer, length, flags) {
        // CoreLib requests the system-preferred generator with no provider handle.
        if ((flags & 2) === 0) return invalidHandle;
        if (algorithm !== 0 || (flags & ~3) !== 0) return invalidParameter;
        const start = pointer >>> 0, count = length >>> 0;
        const buffer = memory.buffer;
        if (start === 0 || start > buffer.byteLength || count > buffer.byteLength - start) return invalidParameter;
        try {
            for (let offset = 0; offset < count; offset += 65536)
                globalThis.crypto.getRandomValues(new Uint8Array(buffer, start + offset, Math.min(65536, count - offset)));
        } catch {
            return unsuccessful;
        }
        return 0;
    }

    return {
        getModuleHandleW,
        loadLibraryExW,
        getProcAddress,
        getFullPathNameW: createFullPathHost(memory, win32FileSystem, errors),
        ...createFilePathHost(memory, win32FileSystem, errors),
        // Only this registry's explicitly bound WNDPROC tokens are callable.
        callWindowProcW(token, hwnd, message, wparam, lparam) {
            return dynamicLibraries.invoke(token, windowProcedureSignature, hwnd, message, wparam, lparam);
        },
        ...createActivationContexts(memory, findManifest, value => { errors.system = value; }),
        ...createDpiAwareness(value => { errors.system = value; }),
        ...displayContexts,
        isWindow: handle => windows.hasWindow(handle) ? 1 : 0,
        getActiveWindow: () => windows.getActiveWindow(),
        setActiveWindow: handle => windows.setActiveWindow(handle, setError),
        getFocus: () => windows.getFocus(),
        setFocus: handle => windows.setFocus(handle, setError),
        isWindowEnabled: handle => windows.isWindowEnabled(handle),
        enableWindow: (handle, enable) => windows.enableWindow(handle, enable),
        getWindowLongW: getWindowLong,
        getWindowLongPtrW: getWindowLong,
        getWindowThreadProcessId,
        setWindowLongW: setWindowLong,
        setWindowLongPtrW: setWindowLong,
        setClassLongW: (hwnd, index, procedure) => classes.setClassProcedure(windows.getWindowClass(hwnd), index, procedure),
        registerClassW: classes.registerClassW,
        getClassInfoW: classes.getClassInfoW,
        createWindowExW: createWindowCreation(memory, windows, classes, bindProcedure, setError, windowCreationMetrics),
        ...createWindowRectQueries(memory, windows, setError),
        redrawWindow: createRedrawWindow(memory, windows, setError),
        ...paint.imports,
        adjustWindowRectExForDpi: createAdjustWindowRectExForDpi(memory, setError),
        ...createThreadHandleHost(memory, () => windows.getCurrentThreadId(), setError),
        ...createThreadMessageQueue(memory, windows, setError, paint.nextPaint),
        notifyWinEvent: createWindowEventPublisher(windows, onWinEvent),
        sendMessageTimeoutW: createWindowSender(memory, windows, setError),
        ...createWindowStationHost(memory, desktop, setError),
        registerWindowMessageW: createWindowMessageRegistration(memory, setError),
        bCryptGenRandom,
        getLastSystemError: () => errors.system,
        setLastSystemError: value => { errors.system = value | 0; },
        getLastPInvokeError: () => errors.pinvoke,
        setLastPInvokeError: value => { errors.pinvoke = value | 0; }
    };
}
