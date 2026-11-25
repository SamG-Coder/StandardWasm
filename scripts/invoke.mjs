export function invoke(exports, method, inputs) {
    const args = inputs.map(input => {
        if (!Array.isArray(input?.bytes)) return input;
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
    if (generated.multiExport) {
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
        const value = exports[generated.export ?? "run"](...generated.arguments);
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
