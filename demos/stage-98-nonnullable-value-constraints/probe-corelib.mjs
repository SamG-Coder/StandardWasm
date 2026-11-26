export async function verifyNullable(compilerBytes, app, invalid, facade, core, oracle) {
    const { instance: { exports: compiler } } = await WebAssembly.instantiate(compilerBytes);

    function upload(bytes) {
        const result = compiler.CreateBytes(bytes.length);
        for (let i = 0; i < bytes.length; i++) compiler.SetByte(result, i, bytes[i]);
        return result;
    }

    function compile(main, row) {
        const images = [main, facade, core].map(image => new Uint8Array(image));
        const bundle = new Uint8Array(images.reduce((size, image) => size + 4 + image.length, 0));
        const offsets = new DataView(bundle.buffer);
        let at = 0;
        for (const image of images) {
            offsets.setUint32(at, image.length, true);
            bundle.set(image, at + 4);
            at += image.length + 4;
        }
        const manifest = new Uint8Array(11);
        new DataView(manifest.buffer).setUint32(0, row, true);
        manifest.set([3, 0, 0, 0, 114, 117, 110], 4);
        const output = compiler.CreateBytes(32768), diagnostic = compiler.CreateDiagnostic();
        const status = compiler.CompileBundleDetailed(upload(bundle), upload(manifest), output, diagnostic);
        const location = [0, 1, 2].map(index => compiler.ReadDiagnostic(diagnostic, index));
        return {
            status, location,
            bytes: status > 0 ? Uint8Array.from({ length: status }, (_, i) => compiler.ReadByte(output, i)) : null
        };
    }

    const valid = compile(app, oracle.row);
    if (!valid.bytes) throw Error("CoreLib Nullable compilation failed at " + valid.location);
    const { instance, module } = await WebAssembly.instantiate(valid.bytes);
    if (WebAssembly.Module.imports(module).length) throw Error("Expected import-free Nullable fixture output");
    for (const { value, expected } of oracle.cases)
        if (instance.exports.run(value) !== expected) throw Error("Nullable value-copy result differed from CLR");

    const rejected = compile(invalid, oracle.rejectedRow);
    if (rejected.status !== -2 || rejected.location.join(",") !== [0, oracle.rejectedRow, -1].join(","))
        throw Error("Unexpected nested Nullable argument result: " + rejected.status + " at " + rejected.location);
    return { cases: oracle.cases.length, rejection: rejected.status, location: rejected.location, bytes: [...valid.bytes] };
}
