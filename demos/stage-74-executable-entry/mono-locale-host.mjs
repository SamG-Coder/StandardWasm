export const monoLocaleModule = "native:libSystem.Globalization.Native";
export const monoLocaleEntry = "GlobalizationNative_IsPredefinedLocale";

// The upstream PAL inspects at most ULOC_FULLNAME_CAPACITY - 1 input units.
const localeUnits = 156;

export function bindMonoLocale(memory, isPredefinedLocale) {
    if (!(memory instanceof WebAssembly.Memory) || typeof isPredefinedLocale !== "function")
        throw TypeError("Mono locale lookup requires guest memory and an ICU provider.");
    return function (_errors, pointer) {
        const start = pointer >>> 0;
        const source = new DataView(memory.buffer);
        if (start === 0 || start % 2 !== 0 || start > source.byteLength - 2)
            throw new WebAssembly.RuntimeError("Invalid Mono locale address.");
        const input = new Uint16Array(localeUnits);
        let count = 0;
        while (count < input.length) {
            const address = start + 2 * count;
            if (address > source.byteLength - 2)
                throw new WebAssembly.RuntimeError("Mono locale input exceeds guest memory.");
            const unit = source.getUint16(address, true);
            if (unit === 0) break;
            input[count++] = unit;
        }
        const result = isPredefinedLocale(input.subarray(0, count));
        if (result !== 0 && result !== 1) throw TypeError("ICU locale lookup must return an integer Boolean.");
        return result;
    };
}

// The sidecar links the stock .NET globalization PAL and ICU libraries. Its
// memory and pinned ICU data are private to this provider.
export function createMonoLocaleIcuProvider(module, icuData) {
    if (!(module instanceof WebAssembly.Module) || !(icuData instanceof Uint8Array) || !icuData.length)
        throw TypeError("Mono locale lookup requires a compiled ICU sidecar and its ICU data.");
    let wasm;
    const imports = {
        env: { emscripten_notify_memory_growth() {} },
        wasi_snapshot_preview1: {
            proc_exit(code) { throw new WebAssembly.RuntimeError("ICU sidecar exited with " + code); },
            environ_sizes_get(count, size) {
                const view = new DataView(wasm.memory.buffer);
                view.setUint32(count, 0, true);
                view.setUint32(size, 0, true);
                return 0;
            },
            environ_get() { return 0; }
        }
    };
    wasm = new WebAssembly.Instance(module, imports).exports;
    wasm._initialize();
    const data = wasm.malloc(icuData.length) >>> 0;
    const scratch = wasm.malloc(2 * (localeUnits + 1)) >>> 0;
    if (!data || !scratch) throw new WebAssembly.RuntimeError("Unable to allocate ICU locale storage.");
    new Uint8Array(wasm.memory.buffer, data, icuData.length).set(icuData);
    if (wasm.installIcuData(data) !== 0) throw new WebAssembly.RuntimeError("ICU data initialization failed.");

    return function (input) {
        if (!(input instanceof Uint16Array) || input.length > localeUnits)
            throw TypeError("ICU expects a bounded UTF-16 locale name.");
        const view = new DataView(wasm.memory.buffer);
        for (let index = 0; index < input.length; index++)
            view.setUint16(scratch + 2 * index, input[index], true);
        view.setUint16(scratch + 2 * input.length, 0, true);
        return wasm[monoLocaleEntry](scratch);
    };
}
