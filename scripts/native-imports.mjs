// Stage 156 (issue #274, decision 0083): the browser host side of native imports.
//
// A compiled module imports each reached P/Invoke, QCall and InternalCall as a typed function:
//   module "native:<dll>"          name = DllImport entry point     (P/Invoke and CoreLib QCall)
//   module "internalcall"          name = "Namespace.Type::Method"
//   module "standardwasm:native"   present, library, clearLastError, saveLastError (host helpers)
// The dynamic imports share a table in import order. For a native call, the module asks
// present(k), then library(k). Routed calls keep their ordinals and resolve separately.
// Native lookup raises CoreLib's
// DllNotFoundException when the registry has no entry at all for that DLL, and
// EntryPointNotFoundException when the DLL is known but lacks the entry. A missing entry is
// therefore never a silent stub and never a raw trap.
//
// registry maps "<dll>!<entry>" (P/Invoke; dll lower-case without ".dll") or "internalcall!<Namespace.Type::Method>" to a
// JavaScript function over Wasm values: i32 as Number, i64 as BigInt, f32/f64 as Number. A pointer
// is an i32 address into the host-lent memory 0, which entries reach through state.memory (Stage 365).

const nativeHelperModule = "standardwasm:native";
export const comImportModule = "standardwasm:com";
export const nativeIndirectImportModule = "standardwasm:native-indirect";
const nativeHelperNames = new Set(["present", "library", "clearLastError", "saveLastError"]);

export function isNativeImportModule(name) {
    return name.startsWith("native:") || name === "internalcall" || name === nativeHelperModule;
}

function isRoutedImportModule(name) {
    return name === comImportModule || name === nativeIndirectImportModule;
}

function nativeDeclarations(module) {
    return WebAssembly.Module.imports(module)
        .filter(entry => isNativeImportModule(entry.module) || isRoutedImportModule(entry.module));
}

// A DLL name as the Windows loader compares it: case-insensitive, with ".dll" optional.
export function normalizeDll(dll) {
    const lower = dll.toLowerCase();
    return lower.endsWith(".dll") ? lower.slice(0, -4) : lower;
}

// The key a registry uses for one import. Entry point names stay case-sensitive, as
// GetProcAddress compares them.
export function nativeKey(module, name) {
    return (module.startsWith("native:") ? normalizeDll(module.slice("native:".length)) : module) + "!" + name;
}

// The native entries a compiled module requires, in table order: [{ module, name, key }].
export function nativeEntries(module) {
    return nativeDeclarations(module).filter(entry => entry.kind === "function")
        .map(entry => ({ module: entry.module, name: entry.name, key: nativeKey(entry.module, entry.name) }));
}

// Import object fragment for `module`. `state` holds the Win32-style last error (system) and the
// CLR's saved P/Invoke error (pinvoke); a registry entry may update state.system.
export function nativeImports(module, registry, state = { system: 0, pinvoke: 0 }) {
    const imports = Object.create(null);
    const helpers = new Set();
    let nativeCount = 0;
    const entries = nativeDeclarations(module).map(({ module: owner, name, kind }) => {
        // Routed calls use their own registries and still occupy a native-table slot.
        if (isRoutedImportModule(owner)) return null;
        const separator = name.lastIndexOf("::");
        if (kind !== "function" || !name || name.includes("\0") || owner.includes("\0") ||
            (owner.startsWith("native:") && !normalizeDll(owner.slice("native:".length))) ||
            (owner === "internalcall" && (separator <= 0 || separator + 2 === name.length)))
            throw Error("The generated module requests an invalid typed native import.");
        if (owner === nativeHelperModule) {
            if (!nativeHelperNames.has(name) || helpers.has(name))
                throw Error("The generated module requests an invalid native runtime helper.");
            helpers.add(name);
        } else nativeCount++;
        return { module: owner, name, key: nativeKey(owner, name) };
    });
    // Keep every declaration in order. Distinct MethodDefs can import the same
    // entry, and their presence checks use individual table slots.
    if ((nativeCount !== 0 || helpers.size !== 0) && (nativeCount === 0 ||
        !helpers.has("present") || !helpers.has("library") ||
        helpers.has("clearLastError") !== helpers.has("saveLastError")))
        throw Error("The generated module requires the complete typed native import contract.");
    const implemented = entries.map(entry => entry !== null &&
        (entry.module === nativeHelperModule || Object.hasOwn(registry, entry.key)));
    // A library is available when the registry implements any of its entries. InternalCalls
    // belong to the runtime itself, which is always loaded.
    const libraries = new Set(Object.keys(registry).map(key => key.slice(0, key.indexOf("!"))));
    const available = entries.map(entry => entry !== null &&
        (!entry.module.startsWith("native:") || libraries.has(entry.key.slice(0, entry.key.indexOf("!")))));
    for (const entry of entries) {
        if (entry === null || entry.module === nativeHelperModule) continue;
        imports[entry.module] ??= Object.create(null);
        imports[entry.module][entry.name] = Object.hasOwn(registry, entry.key)
            ? (...args) => registry[entry.key](state, ...args)
            // Unreachable from compiled code, which checks present(k) first; kept explicit.
            : () => { throw new Error("StandardWasm: native entry " + entry.key + " is not implemented by this host"); };
    }
    if (helpers.size !== 0) {
        imports[nativeHelperModule] = {
            present: k => implemented[k] ? 1 : 0,
            library: k => available[k] ? 1 : 0,
            // The CLR's SetLastError=true stub: clear the system error, call, then save it.
            clearLastError: () => { state.system = 0; },
            saveLastError: () => { state.pinvoke = state.system; },
        };
    }
    return imports;
}

// Stage 365 (decision 0083, step 2): a pointer argument or result is a wasm32 address into the
// module's memory 0, which the host lends as imports["standardwasm:resources"].memory. An entry
// that dereferences one reads state.memory, which the host sets to that same WebAssembly.Memory.
// Each call makes a fresh view, because memory.grow detaches the previous buffer.
//
// On Windows, a callee that dereferences null or an unmapped address raises an access violation,
// which the CLR does not turn into a catchable exception: the process ends. Here the entry throws
// a host Error instead, which no managed catch clause can intercept either.
function nativeBytes(state, address, length) {
    if (!(state.memory instanceof WebAssembly.Memory))
        throw Error("StandardWasm: a native entry dereferences a pointer, but the host set no state.memory");
    const bytes = new Uint8Array(state.memory.buffer);
    const start = address >>> 0;
    if (start === 0 || start + length > bytes.length)
        throw Error("StandardWasm: access violation at native address " + start);
    return bytes;
}

const fileTimeUnixEpochMilliseconds = 11644473600000n; // 1601-01-01 to 1970-01-01
const errorInvalidParameter = 87;

// FileTimeToSystemTime: 100 ns ticks since 1601-01-01 UTC into a SYSTEMTIME, truncated to whole
// milliseconds. Values with the top bit set fail with ERROR_INVALID_PARAMETER and leave the
// output untouched.
function fileTimeToSystemTime(state, fileTime, systemTime) {
    nativeBytes(state, fileTime, 8);
    const bytes = nativeBytes(state, systemTime, 16);
    const view = new DataView(bytes.buffer);
    const ticks = view.getBigUint64(fileTime >>> 0, true);
    if (ticks > 0x7fffffffffffffffn) { state.system = errorInvalidParameter; return 0; }
    // At most 9.1e14 ms past 1970, inside both Number's exact range and Date's.
    const date = new Date(Number(ticks / 10000n - fileTimeUnixEpochMilliseconds));
    const fields = [date.getUTCFullYear(), date.getUTCMonth() + 1, date.getUTCDay(), date.getUTCDate(),
        date.getUTCHours(), date.getUTCMinutes(), date.getUTCSeconds(), date.getUTCMilliseconds()];
    fields.forEach((value, i) => view.setUint16((systemTime >>> 0) + 2 * i, value, true));
    return 1;
}

// SystemTimeToFileTime: validates every field except wDayOfWeek (ignored) against the documented
// SYSTEMTIME range. A field out of range fails with ERROR_INVALID_PARAMETER and leaves the output
// untouched.
function systemTimeToFileTime(state, systemTime, fileTime) {
    const bytes = nativeBytes(state, systemTime, 16);
    nativeBytes(state, fileTime, 8);
    const view = new DataView(bytes.buffer);
    const [year, month, , day, hour, minute, second, milliseconds] =
        Array.from({ length: 8 }, (_, i) => view.getUint16((systemTime >>> 0) + 2 * i, true));
    const monthDays = month === 2 ? (year % 4 === 0 && (year % 100 !== 0 || year % 400 === 0) ? 29 : 28)
        : [4, 6, 9, 11].includes(month) ? 30 : 31;
    if (year < 1601 || year > 30827 || month < 1 || month > 12 || day < 1 || day > monthDays ||
        hour > 23 || minute > 59 || second > 59 || milliseconds > 999) {
        state.system = errorInvalidParameter;
        return 0;
    }
    const unix = BigInt(Date.UTC(year, month - 1, day, hour, minute, second, milliseconds));
    view.setBigUint64(fileTime >>> 0, (unix + fileTimeUnixEpochMilliseconds) * 10000n, true);
    return 1;
}

// C strlen over memory 0. Running past the end of memory is the access violation above.
function stringLength(state, text) {
    const bytes = nativeBytes(state, text, 1);
    const start = text >>> 0;
    const end = bytes.indexOf(0, start);
    if (end < 0) throw Error("StandardWasm: access violation at native address " + bytes.length);
    return end - start;
}

// C memcpy: copies count bytes and returns the destination. Zero bytes touch neither pointer, so
// null operands are allowed then, as in ucrtbase. Overlapping ranges are undefined in C; this
// copy behaves like memmove.
function memoryCopy(state, destination, source, count) {
    const length = count >>> 0;
    if (length === 0) return destination;
    nativeBytes(state, source, length);
    const bytes = nativeBytes(state, destination, length);
    bytes.copyWithin(destination >>> 0, source >>> 0, (source >>> 0) + length);
    return destination;
}

// Pointer entries the Stage 365 fixture uses, with the semantics of x86 kernel32 and ucrtbase.
// A host combines them with windowsScalars and sets state.memory.
export const windowsPointers = Object.freeze({
    "kernel32!FileTimeToSystemTime": fileTimeToSystemTime,
    "kernel32!SystemTimeToFileTime": systemTimeToFileTime,
    "ucrtbase!strlen": stringLength,
    "ucrtbase!memcpy": memoryCopy,
});

// MulDiv(number, numerator, denominator): the 64-bit product divided with rounding half away
// from zero; -1 when the denominator is zero or the result does not fit in 32 bits.
function mulDiv(number, numerator, denominator) {
    if (denominator === 0) return -1;
    const product = BigInt(number) * BigInt(numerator), divisor = BigInt(denominator);
    const negative = (product < 0n) !== (divisor < 0n);
    const magnitude = product < 0n ? -product : product, by = divisor < 0n ? -divisor : divisor;
    const quotient = (2n * magnitude + by) / (2n * by);
    const result = negative ? -quotient : quotient;
    return result < -2147483648n || result > 2147483647n ? -1 : Number(result);
}

// Decision 0104: the kernel32 clock behind Stopwatch. Both write a LARGE_INTEGER through the pointer
// into memory 0 and return TRUE. The counter is monotonic nanoseconds (Node's hrtime, or a browser's
// performance.now at its own resolution), so the frequency is 1e9.
const hrtime = globalThis.process?.hrtime?.bigint;
const nanoseconds = hrtime ? () => hrtime() : () => BigInt(Math.round(performance.now() * 1e6));
function writeLargeInteger(state, address, value) {
    const bytes = nativeBytes(state, address, 8);
    new DataView(bytes.buffer).setBigInt64(address >>> 0, value, true);
    return 1;
}
export const windowsClock = Object.freeze({
    "kernel32!QueryPerformanceCounter": (state, address) => writeLargeInteger(state, address, nanoseconds()),
    "kernel32!QueryPerformanceFrequency": (state, address) => writeLargeInteger(state, address, 1000000000n),
});

// Scalar Windows entries the Stage 156 fixture uses, with the C semantics of x86 kernel32 and
// ucrtbase, plus the CoreLib InternalCalls behind Marshal.GetLastWin32Error.
export const windowsScalars = Object.freeze({
    "kernel32!MulDiv": (state, number, numerator, denominator) => mulDiv(number, numerator, denominator),
    "kernel32!GetLastError": state => state.system,
    "kernel32!SetLastError": (state, code) => { state.system = code | 0; },
    "ucrtbase!abs": (state, value) => (value < 0 ? -value : value) | 0,
    "ucrtbase!llabs": (state, value) => value < 0n ? BigInt.asIntN(64, -value) : value,
    "ucrtbase!sqrt": (state, value) => Math.sqrt(value),
    // C fmaxf returns the other operand when one is NaN.
    "ucrtbase!fmaxf": (state, left, right) =>
        Number.isNaN(left) ? right : Number.isNaN(right) ? left : Math.fround(Math.max(left, right)),
    // C roundf rounds halfway cases away from zero (Math.round rounds them up).
    "ucrtbase!roundf": (state, value) =>
        Number.isFinite(value) ? Math.fround(Math.sign(value) * Math.floor(Math.abs(value) + 0.5)) : value,
    "ucrtbase!copysignf": (state, magnitude, sign) =>
        Math.fround(Math.abs(magnitude) * (sign < 0 || Object.is(sign, -0) ? -1 : 1)),
    "internalcall!System.Runtime.InteropServices.Marshal::GetLastPInvokeError": state => state.pinvoke,
    "internalcall!System.Runtime.InteropServices.Marshal::SetLastPInvokeError": (state, error) => { state.pinvoke = error | 0; },
});
