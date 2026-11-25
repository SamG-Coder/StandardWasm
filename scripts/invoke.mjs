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
