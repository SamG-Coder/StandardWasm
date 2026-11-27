const moduleNotFound = 126, entryPointNotFound = 127, invalidHandle = 6, invalidParameter = 87;
const searchSystem32 = 0x800;
const invalidValue = Symbol();
let nextId = 0x40000000;

function id() {
    if (nextId > 0x7fffffff) throw new RangeError("StandardWasm: exhausted dynamic library handles");
    return nextId++;
}

function libraryKey(name) {
    if (typeof name !== "string" || !/^[^\\/:*?"<>|\u0000-\u001f]+$/u.test(name) ||
        name.endsWith(".") || name.endsWith(" "))
        return undefined;
    const lower = name.toLowerCase();
    return lower.includes(".") ? lower : lower + ".dll";
}

function validEntryName(name) {
    return typeof name === "string" && name.length !== 0 && !name.includes("\0");
}

const types = new Map([
    ["i32", value => Number.isInteger(value) && (value | 0) === value ? value : invalidValue],
    ["u32", unsigned32],
    ["p32", unsigned32],
    ["i64", value => typeof value === "bigint" && BigInt.asIntN(64, value) === value ? value : invalidValue],
    ["u64", value => typeof value === "bigint" && value >= -(1n << 63n) && value < (1n << 64n)
        ? BigInt.asUintN(64, value) : invalidValue],
    ["f32", value => typeof value === "number" ? Math.fround(value) : invalidValue],
    ["f64", value => typeof value === "number" ? value : invalidValue],
    ["void", value => value === undefined ? value : invalidValue]
]);

function unsigned32(value) {
    return Number.isInteger(value) && value >= -0x80000000 && value <= 0xffffffff ? value >>> 0 : invalidValue;
}

function parseSignature(signature) {
    const match = typeof signature === "string" &&
        /^(?:platform|stdcall|cdecl):([a-z0-9]+)\(([a-z0-9,]*)\)$/u.exec(signature);
    if (!match) return undefined;
    const result = types.get(match[1]);
    const parameters = match[2] ? match[2].split(",").map(name => name === "void" ? undefined : types.get(name)) : [];
    return result && parameters.every(Boolean) ? { parameters, result } : undefined;
}

// An instance owns explicit browser capabilities and their opaque handles.
// Loading a name never accesses the operating system or browser filesystem.
export function createDynamicLibraryHost(errors) {
    const libraries = new Map();
    const handles = new Map();
    const targets = new Map();

    function failure(code) {
        errors.system = code;
        return 0;
    }

    function registerLibrary(name, entries) {
        const key = libraryKey(name);
        if (key === undefined || libraries.has(key) || !(entries instanceof Map) || entries.size === 0)
            throw new TypeError("StandardWasm: invalid dynamic library registration");

        const exports = new Map();
        for (const [entry, method] of entries) {
            const signature = method?.signature;
            const invoke = method?.invoke;
            const shape = parseSignature(signature);
            if (!validEntryName(entry) || typeof invoke !== "function" || !shape)
                throw new TypeError("StandardWasm: invalid dynamic export registration");
            exports.set(entry, { signature, invoke, shape, library: key, entry });
        }
        libraries.set(key, { exports, handle: 0, references: 0, tokens: new Map() });
    }

    function loadLibrary(name, reserved = 0, flags = 0) {
        if (reserved !== 0 || (flags !== 0 && flags !== searchSystem32))
            return failure(invalidParameter);
        const library = libraries.get(libraryKey(name));
        if (!library) return failure(moduleNotFound);

        if (!library.handle) {
            library.handle = id();
            handles.set(library.handle, library);
        }
        library.references++;
        return library.handle;
    }

    function getModuleHandle(name) {
        return libraries.get(libraryKey(name))?.handle || failure(moduleNotFound);
    }

    function getProcAddress(handle, name) {
        const library = handles.get(handle);
        if (!library) return failure(invalidHandle);
        const target = library.exports.get(name);
        if (!target) return failure(entryPointNotFound);
        const previous = library.tokens.get(name);
        if (previous !== undefined) return previous;

        const token = id();
        targets.set(token, target);
        library.tokens.set(name, token);
        return token;
    }

    function freeLibrary(handle) {
        const library = handles.get(handle);
        if (!library) return failure(invalidHandle);
        if (--library.references === 0) {
            for (const token of library.tokens.values()) targets.delete(token);
            library.tokens.clear();
            handles.delete(handle);
            library.handle = 0;
        }
        return 1;
    }

    function invoke(token, signature, ...args) {
        const target = targets.get(token);
        if (!target) throw new Error("StandardWasm: unregistered dynamic export token");
        const label = target.library + "!" + target.entry;
        const { parameters, result } = target.shape;
        if (target.signature !== signature || args.length !== parameters.length)
            throw new TypeError("StandardWasm: dynamic export signature mismatch for " + label);
        const values = args.map((argument, index) => parameters[index](argument));
        if (values.includes(invalidValue))
            throw new TypeError("StandardWasm: invalid dynamic export argument for " + label);
        const value = result(target.invoke(...values));
        if (value === invalidValue)
            throw new TypeError("StandardWasm: invalid dynamic export result for " + label);
        return value;
    }

    return { registerLibrary, loadLibrary, getModuleHandle, getProcAddress, freeLibrary, invoke };
}
