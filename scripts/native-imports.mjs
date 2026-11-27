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
// JavaScript function over Wasm values: i32 as Number, i64 as BigInt, f32/f64 as Number.

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
