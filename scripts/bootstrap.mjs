import { invokeGenerated } from "./invoke.mjs";

export async function verifyBootstrap(host, fixture, cases) {
    const [dll, row] = fixture.inputs;
    const image = host.CreateBytes(dll.bytes.length);
    for (let i = 0; i < dll.bytes.length; i++) host.SetByte(image, i, dll.bytes[i]);

    async function build(compile) {
        const output = host.CreateBytes(1 << 20);
        const length = compile(image, row, output);
        if (length <= 0 || length > (1 << 20))
            throw Error("Self-compilation returned " + length);
        const bytes = Uint8Array.from({ length }, (_, i) => host.ReadByte(output, i));
        const module = await WebAssembly.compile(bytes);
        if (WebAssembly.Module.imports(module).length) throw Error("Compiler has runtime imports");
        const { exports } = await WebAssembly.instantiate(module);
        return { bytes, compile: exports.run };
    }

    const first = await build(host.Compile);
    const second = await build(first.compile);
    if (second.bytes.length !== first.bytes.length || !first.bytes.every((b, i) => second.bytes[i] === b))
        throw Error("Self-recompiled compiler differs from the first generation");

    // The bootstrap module supplies only GC buffer transport. Every compilation
    // below is performed by the module emitted by the first portable generation.
    const compiler = {
        CreateBytes: host.CreateBytes, SetByte: host.SetByte, ReadByte: host.ReadByte,
        FindRow: host.FindRow, Compile: second.compile
    };
    let tested = 0;
    for (const c of cases) {
        if (!c.generated) continue;
        const actual = await invokeGenerated(compiler, c.inputs, c.generated);
        if (actual !== c.expected)
            throw Error("Self-recompiled compiler failed " + c.module + " " + c.fixture);
        tested++;
    }
    return second.bytes.length + " byte-identical compiler bytes and " + tested + " generated-code cases";
}
