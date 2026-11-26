// JSON carries a 64-bit integer as {"i64": "<signed decimal>"} because a JSON number is exact only to 2^53.
// The Wasm JS API passes i64 parameters and results as BigInt, so decode those values before calling or comparing.
// A floating-point value is {"f64": "<17 significant digits>"}, which also covers "NaN", "Infinity", "-Infinity" and
// "-0". JSON cannot carry those. f32 values are encoded through their exact double value.
export const decode = value =>
    value?.i64 !== undefined ? BigInt(value.i64) : value?.f64 !== undefined ? Number(value.f64) : value;

const managedTags = [
    ["\0overflow", "System.OverflowException"],
    ["\0divideByZero", "System.DivideByZeroException"],
    ["\0indexOutOfRange", "System.IndexOutOfRangeException"],
    ["\0nullReference", "System.NullReferenceException"],
    ["\0invalidCast", "System.InvalidCastException"]
];

export function managedExceptionType(exports, error) {
    if (!(error instanceof WebAssembly.Exception)) return null;
    for (const [name, type] of managedTags) {
        const tag = exports[name];
        if (tag instanceof WebAssembly.Tag && error.is(tag)) return type;
    }
    return null;
}

export function invoke(exports, method, inputs) {
    const args = inputs.map(input => {
        if (!Array.isArray(input?.bytes)) return decode(input);
        // Typed Wasm GC arrays are opaque to JavaScript. Populate the array with fixture exports.
        const buffer = exports.CreateBytes(input.bytes.length);
        for (const [index, value] of input.bytes.entries()) exports.SetByte(buffer, index, value);
        return buffer;
    });
    return exports[method](...args);
}

export async function invokeGenerated(exports, inputs, generated) {
    const output = exports.CreateBytes(generated.capacity);
    if (generated.reject)
        for (let i = 0; i < generated.capacity; i++) exports.SetByte(output, i, 0xa5);
    // The host only moves bytes and asks the engine to validate and execute the result.
    const [image, selection] = inputs;
    let length;
    if (generated.bundle) {
        let entries = selection;
        if (Array.isArray(selection?.name)) {
            const bundle = Uint8Array.from(image.bytes);
            const length = new DataView(bundle.buffer).getUint32(0, true);
            const primary = { bytes: Array.from(bundle.subarray(4, 4 + length)) };
            const row = invoke(exports, "FindRow", [primary, { bytes: selection.name }]);
            if (row < 1) throw Error("Bundle fixture method was not found");
            const manifest = new Uint8Array(11);
            new DataView(manifest.buffer).setUint32(0, row, true);
            manifest[4] = 3;
            manifest.set([114, 117, 110], 8);
            entries = { bytes: Array.from(manifest) };
        }
        length = invoke(exports, "CompileBundle", [image, entries, output]);
    } else if (generated.multiExport) {
        length = invoke(exports, "CompileModule", [image, selection, output]);
    } else {
        const row = Array.isArray(selection?.name)
            ? invoke(exports, "FindRow", [image, { bytes: selection.name }])
            : selection;
        if (Array.isArray(selection?.name) && row < 1) throw Error("Fixture method was not found");
        length = invoke(exports, "Compile", [image, row, output]);
    }
    if (generated.reject) {
        if (length >= 0) throw Error("Expected a compiler diagnostic");
        for (let i = 0; i < generated.capacity; i++)
            if (exports.ReadByte(output, i) !== 0xa5) throw Error("Diagnostic modified the output buffer");
        return length;
    }
    if (length <= 0 || length > generated.capacity) throw Error("Compiler returned invalid length: " + length);
    const bytes = new Uint8Array(length);
    for (let i = 0; i < length; i++) bytes[i] = exports.ReadByte(output, i);
    if (generated.hex !== undefined) {
        const hex = Array.from(bytes, value => value.toString(16).padStart(2, "0")).join("");
        if (hex !== generated.hex) throw Error("Emitted bytes differ from the CLR compiler");
    }
    const { instance, module } = await WebAssembly.instantiate(bytes);
    if (WebAssembly.Module.imports(module).length) throw Error("Generated module has runtime imports");
    try {
        const exports = instance.exports;
        const value = exports[generated.export ?? "run"](...generated.arguments.map(decode));
        if (generated.resultType !== "string") return value;
        const count = exports.TextLength(value);
        const units = new Array(count);
        for (let index = 0; index < count; index++)
            units[index] = String.fromCharCode(exports.TextCodeUnit(value, index));
        return units.join("");
    } catch (error) {
        if (!(error instanceof WebAssembly.RuntimeError)) throw error;
        return null;
    }
}
