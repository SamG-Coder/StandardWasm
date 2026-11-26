// Represent signed i64 bit patterns as decimal strings to keep the JS boundary exact.
export const decode = value => value?.i64 !== undefined ? BigInt(value.i64)
    : value?.f64 !== undefined ? Number(value.f64) : value;

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

export async function invokeGenerated(exports, inputs, generated, reportDiagnostic) {
    const output = exports.CreateBytes(generated.capacity);
    if (generated.reject)
        for (let i = 0; i < generated.capacity; i++) exports.SetByte(output, i, 0xa5);
    // The host only moves bytes and asks the engine to validate and execute the result.
    const [image, selection] = inputs;
    const location = generated.diagnostic ? exports.CreateDiagnostic() : undefined;
    const compile = (method, args) => location
        ? invoke(exports, method + "Detailed", [...args, location])
        : invoke(exports, method, args);
    let length, entryRow;
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
        length = compile("CompileBundle", [image, entries, output]);
    } else if (generated.multiExport) {
        length = compile("CompileModule", [image, selection, output]);
    } else {
        entryRow = Array.isArray(selection?.name)
            ? invoke(exports, "FindRow", [image, { bytes: selection.name }])
            : selection;
        if (Array.isArray(selection?.name) && entryRow < 1) throw Error("Fixture method was not found");
        length = compile("Compile", [image, entryRow, output]);
    }
    if (location) {
        const expected = generated.diagnostic.map(value => value === "entry" ? entryRow : value);
        const actual = expected.map((_, index) => exports.ReadDiagnostic(location, index));
        if (actual.some((value, index) => value !== expected[index]))
            throw Error("Diagnostic location " + actual + " differs from expected " + expected);
        reportDiagnostic?.(actual);
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
