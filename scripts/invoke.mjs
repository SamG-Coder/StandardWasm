// JSON carries a 64-bit integer as {"i64": "<signed decimal>"} because a JSON number is exact only to 2^53.
// The Wasm JS API passes i64 parameters and results as BigInt, so decode those values before calling or comparing.
// A floating-point value is {"f64": "<17 significant digits>"}, which also covers "NaN", "Infinity", "-Infinity" and
// "-0". JSON cannot carry those. f32 values are encoded through their exact double value.
export const decode = value =>
    value?.i64 !== undefined ? BigInt(value.i64) : value?.f64 !== undefined ? Number(value.f64) : value;

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
