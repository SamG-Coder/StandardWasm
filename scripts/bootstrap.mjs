import { invokeGenerated } from "./invoke.mjs";

export async function verifyBootstrap(initial, fixture, cases, saveCompiler) {
    const dll = fixture.inputs[0].bytes;
    const manifest = fixture.inputs[1].bytes;

    async function build(compiler) {
        function upload(values) {
            const array = compiler.CreateBytes(values.length);
            for (let i = 0; i < values.length; i++) compiler.SetByte(array, i, values[i]);
            return array;
        }

        const capacity = 1 << 20;
        const output = compiler.CreateBytes(capacity);
        const length = compiler.CompileModule(upload(dll), upload(manifest), output);
        if (length <= 0 || length > capacity)
            throw Error("Self-compilation returned " + length);
        const bytes = Uint8Array.from({ length }, (_, i) => compiler.ReadByte(output, i));
        const module = await WebAssembly.compile(bytes);
        if (WebAssembly.Module.imports(module).length) throw Error("Compiler has runtime imports");
        const { exports } = await WebAssembly.instantiate(module);
        for (const name of fixture.exports)
            if (typeof exports[name] !== "function") throw Error("Compiler lacks export " + name);
        return { bytes, exports };
    }

    const first = await build(initial);
    // No references allocated by the initial Wasm instance are passed onward.
    const second = await build(first.exports);
    if (second.bytes.length !== first.bytes.length || !first.bytes.every((b, i) => second.bytes[i] === b))
        throw Error("Self-recompiled compiler differs from the first generation");

    let tested = 0;
    for (const c of cases) {
        if (!c.generated) continue;
        const actual = await invokeGenerated(second.exports, c.inputs, c.generated);
        if (actual !== c.expected)
            throw Error("Self-recompiled compiler failed " + c.module + " " + c.fixture);
        tested++;
    }
    if (saveCompiler) saveCompiler(second.bytes);
    return second.bytes.length + " byte-identical compiler bytes and " + tested + " generated-code cases";
}
