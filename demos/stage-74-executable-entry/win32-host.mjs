export const win32ImportModule = "standardwasm:win32";
export const win32ImportNames = new Set([
    "getModuleHandleW", "getLastSystemError", "setLastSystemError", "getLastPInvokeError", "setLastPInvokeError"
]);

const moduleNotFound = 126;
const noAccess = 998;
const executableHandle = 1;
const decoder = new TextDecoder("utf-16le", { ignoreBOM: true });
const normalize = name => name.replaceAll("\\", "/").toLowerCase();

export function createWin32Host(memory, executablePath) {
    const executable = executablePath ? normalize(executablePath) : "";
    const baseName = executable.slice(executable.lastIndexOf("/") + 1);
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
            const leaf = name.slice(name.lastIndexOf("/") + 1);
            if (!leaf.includes(".")) name += ".dll";
            else if (leaf.endsWith(".")) name = name.slice(0, -1);
            if (executable && (name.includes("/") ? name === executable : name === baseName))
                return executableHandle;
        }
        // This process has its own executable handle. Managed companion
        // assemblies are resolved by the managed loader, not as native DLLs.
        systemError = moduleNotFound;
        return 0;
    }

    return {
        getModuleHandleW,
        getLastSystemError: () => systemError,
        setLastSystemError: value => { systemError = value | 0; },
        getLastPInvokeError: () => pinvokeError,
        setLastPInvokeError: value => { pinvokeError = value | 0; }
    };
}
