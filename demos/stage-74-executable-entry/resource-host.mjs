import { createWin32Host, win32ImportModule, win32ImportNames, win32MemoryImports } from "./win32-host.mjs";

export const resourceImportModule = "standardwasm:resources";

const pageSize = 65536;
const nullName = -8n, emptyName = -9n, malformed = -1n;
const createMemory = () => new WebAssembly.Memory({ initial: 1 });

// The module asks for writable scratch for its UTF-16 name. Successful lookups
// return a stable address in this memory, which stays alive with the instance.
export function createResourceHost(compiler, images) {
    const memory = createMemory();
    const handles = new Array(images.length);
    const resources = images.map(() => new Map());
    const decoder = new TextDecoder("utf-16le", { ignoreBOM: true });
    const encoder = new TextEncoder();
    let next = 0, limit = 0;
    let scratch = 0;
    let scratchCapacity = 0;

    function upload(bytes) {
        const result = compiler.CreateBytes(bytes.length);
        for (let i = 0; i < bytes.length; i++) compiler.SetByte(result, i, bytes[i]);
        return result;
    }

    function allocate(bytes) {
        const length = Math.max(1, bytes);
        let address = Math.ceil(next / 8) * 8;
        if (length > limit - address) {
            // Claim newly appended pages. The application's memory allocator
            // may also grow memory and owns pages that it appended itself.
            const pages = Math.ceil(length / pageSize);
            address = memory.grow(pages) * pageSize;
            limit = address + pages * pageSize;
        }
        next = address + length;
        return address;
    }

    function reserveName(units) {
        if (!Number.isInteger(units) || units < 0) throw RangeError("Invalid managed resource name length");
        if (!scratch || units > scratchCapacity) {
            const capacity = Math.max(32, units, Math.min(0x7fffffff, scratchCapacity * 2));
            scratch = allocate(capacity * 2);
            scratchCapacity = capacity;
        }
        return scratch;
    }

    function locate(owner, pointer, units) {
        const address = pointer >>> 0;
        if (address === 0) return nullName;
        if (!Number.isInteger(units) || units < 0 || units > scratchCapacity || address !== scratch ||
            !Number.isInteger(owner) || owner < 0 || owner >= images.length) return malformed;

        // CoreCLR's Windows QCall directly receives a NUL-terminated name.
        const nameUnits = new DataView(memory.buffer, address, units * 2);
        let length = 0;
        while (length < units && nameUnits.getUint16(length * 2, true) !== 0) length++;
        if (!length) return emptyName;
        const name = decoder.decode(new Uint8Array(memory.buffer, address, length * 2));
        const cache = resources[owner];
        const previous = cache.get(name);
        if (previous !== undefined) return previous;

        const image = images[owner];
        const handle = handles[owner] ??= upload(image);
        const range = compiler.FindEmbeddedResource(handle, upload(encoder.encode(name)));
        if (range < 0n) return range;
        const offset = Number(range & 0xffffffffn);
        const bytes = Number(range >> 32n);
        if (offset > image.length || bytes > image.length - offset) return malformed;

        const payload = allocate(bytes);
        new Uint8Array(memory.buffer, payload, bytes).set(image.subarray(offset, offset + bytes));
        const packed = (BigInt(bytes) << 32n) | BigInt(payload);
        cache.set(name, packed);
        return packed;
    }

    return { memory, imports: { [resourceImportModule]: { memory, reserveName, locate } } };
}

const threadingModule = "standardwasm:threading";
const runtimeModule = "standardwasm:runtime";

export function createMonitorHost() {
    // Compiled managed execution is synchronous on one Wasm agent. Identity
    // stays in the GC, and unused monitor keys can be collected.
    const depth = new WeakMap();
    return {
        changeMonitorDepth(target, delta) {
            const current = depth.get(target) ?? 0;
            if (delta === -1 && current === 0) return 0;
            const next = current + delta;
            if (next === 0) depth.delete(target);
            else depth.set(target, next);
            return 1;
        }
    };
}

const resourceImportKinds = new Map([
    ["memory", "memory"], ["reserveName", "function"], ["locate", "function"]
]);

export function instantiateManagedModule(module, compiler, assets, runtimeImports = {}, executablePath, windows) {
    const requestedResources = new Set();
    const requestedWin32 = new Set();
    let wantsMonitor = false;
    let wantsDebugger = false;
    for (const { module: owner, name, kind } of WebAssembly.Module.imports(module)) {
        if (owner === resourceImportModule) {
            if (resourceImportKinds.get(name) !== kind || requestedResources.has(name))
                throw Error("The generated module requests an invalid resource import.");
            requestedResources.add(name);
            continue;
        }
        if (owner === threadingModule) {
            if (kind !== "function" || name !== "changeMonitorDepth" || wantsMonitor)
                throw Error("The generated module requests an invalid monitor import.");
            wantsMonitor = true;
            continue;
        }
        if (owner === runtimeModule) {
            if (kind !== "function" || name !== "isDebuggerAttached" || wantsDebugger)
                throw Error("The generated module requests an invalid managed runtime import.");
            wantsDebugger = true;
            continue;
        }
        if (owner === win32ImportModule) {
            if (kind !== "function" || !win32ImportNames.has(name) || requestedWin32.has(name))
                throw Error("The generated module requests an invalid Win32 import.");
            requestedWin32.add(name);
            continue;
        }
        const group = Object.hasOwn(runtimeImports, owner) ? runtimeImports[owner] : undefined;
        if (!group || kind !== "function" || !Object.hasOwn(group, name) || typeof group[name] !== "function")
            throw Error("The generated module requests an unsupported runtime import.");
    }
    if ([...requestedWin32].some(name => win32MemoryImports.has(name)) && !requestedResources.has("memory"))
        throw Error("The requested Win32 functions require the StandardWasm memory import.");

    let memory;
    let imports = { ...runtimeImports };
    if (requestedResources.size === 1 && requestedResources.has("memory")) {
        memory = createMemory();
        imports[resourceImportModule] = { memory };
    } else if (requestedResources.size) {
        if (requestedResources.size !== resourceImportKinds.size || typeof compiler.FindEmbeddedResource !== "function")
            throw Error("The generated module requires the complete StandardWasm resource contract.");
        // The bundle owner indices use this same image order, starting at zero.
        const resource = createResourceHost(compiler, assets.map(asset => asset.bytes));
        memory = resource.memory;
        imports = { ...imports, ...resource.imports };
    }
    if (requestedWin32.size) {
        const usesModules = requestedWin32.has("getModuleHandleW") || requestedWin32.has("createActCtxW");
        imports[win32ImportModule] = createWin32Host(memory, executablePath, usesModules ? assets : [], windows);
    }
    if (wantsMonitor) imports[threadingModule] = createMonitorHost();
    if (wantsDebugger) {
        const debuggerHost = Object.hasOwn(runtimeImports, runtimeModule) ? runtimeImports[runtimeModule] : undefined;
        // A host without a managed debugger reports the ordinary detached state.
        imports[runtimeModule] = {
            isDebuggerAttached: () => typeof debuggerHost?.isDebuggerAttached === "function" && debuggerHost.isDebuggerAttached() ? 1 : 0
        };
    }
    return WebAssembly.instantiate(module, imports);
}
