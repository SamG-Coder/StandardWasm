// Stage 156 (issue #274, decision 0083): the browser host side of native imports.
//
// A compiled module imports each reached P/Invoke, QCall and InternalCall as a typed function:
//   module "native:<dll>"          name = DllImport entry point     (P/Invoke and CoreLib QCall)
//   module "internalcall"          name = "Namespace.Type::Method"
//   module "standardwasm:native"   present, clearLastError, saveLastError (host helpers)
// These imports form one table in import order; entry k is the k-th of them. Before each call
// the module asks present(k). For an entry the registry lacks, it raises CoreLib's
// EntryPointNotFoundException with the CLR's message, as the CLR does at a call whose entry
// point is missing. A missing entry is therefore never a silent stub and never a raw trap.
//
// registry maps "<dll>!<entry>" (P/Invoke) or "internalcall!<Namespace.Type::Method>" to a
// JavaScript function over Wasm values: i32 as Number, i64 as BigInt, f32/f64 as Number.

const nativeModule = name => name.startsWith("native:") || name === "internalcall" || name === "standardwasm:native";

// The key a registry uses for one import.
export function nativeKey(module, name) {
    return (module.startsWith("native:") ? module.slice("native:".length) : module) + "!" + name;
}

// The native entries a compiled module requires, in table order: [{ module, name, key }].
export function nativeEntries(module) {
    return WebAssembly.Module.imports(module)
        .filter(entry => entry.kind === "function" && nativeModule(entry.module))
        .map(entry => ({ module: entry.module, name: entry.name, key: nativeKey(entry.module, entry.name) }));
}

// Import object fragment for `module`. `state` holds the Win32-style last error (system) and the
// CLR's saved P/Invoke error (pinvoke); a registry entry may update state.system.
export function nativeImports(module, registry, state = { system: 0, pinvoke: 0 }) {
    const imports = {};
    const entries = nativeEntries(module);
    const implemented = entries.map(entry => entry.module === "standardwasm:native" || Object.hasOwn(registry, entry.key));
    for (const entry of entries) {
        imports[entry.module] ??= {};
        if (entry.module === "standardwasm:native") continue;
        imports[entry.module][entry.name] = Object.hasOwn(registry, entry.key)
            ? (...args) => registry[entry.key](state, ...args)
            // Unreachable from compiled code, which checks present(k) first; kept explicit.
            : () => { throw new Error("StandardWasm: native entry " + entry.key + " is not implemented by this host"); };
    }
    if (entries.some(entry => entry.module === "standardwasm:native")) {
        imports["standardwasm:native"] = {
            present: k => implemented[k] ? 1 : 0,
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
