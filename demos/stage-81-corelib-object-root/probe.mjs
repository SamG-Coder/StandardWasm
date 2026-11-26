export async function verifyCoreRoot(compilerBytes, app, facade, runtimeFacade, core, oracle) {
    const { instance: { exports: compiler } } = await WebAssembly.instantiate(compilerBytes);
    const upload = bytes => {
        const target = compiler.CreateBytes(bytes.length);
        bytes.forEach((value, index) => compiler.SetByte(target, index, value));
        return target;
    };
    const manifest = new Uint8Array(11);
    const entries = new DataView(manifest.buffer);
    entries.setUint32(0, oracle.method, true);
    entries.setUint32(4, 3, true);
    manifest.set([114, 117, 110], 8);
    const assets = [app, facade, runtimeFacade, core];
    const packed = new Uint8Array(assets.reduce((length, bytes) => length + bytes.length + 4, 0));
    const lengths = new DataView(packed.buffer);
    let index = 0;
    for (const bytes of assets) {
        lengths.setUint32(index, bytes.length, true);
        packed.set(bytes, index + 4);
        index += bytes.length + 4;
    }
    const output = compiler.CreateBytes(1 << 20);
    const diagnostic = compiler.CreateDiagnostic();
    const size = compiler.CompileBundleDetailed(upload(packed), upload(manifest), output, diagnostic);
    if (size <= 0) throw Error("CoreLib fixture returned " + size + " at " +
        [0, 1, 2].map(index => compiler.ReadDiagnostic(diagnostic, index)).join(","));
    const bytes = Uint8Array.from({ length: size }, (_, i) => compiler.ReadByte(output, i));
    const { instance } = await WebAssembly.instantiate(bytes);
    const actual = oracle.arguments.map(value => instance.exports.run(value));
    if (actual.some((value, i) => value !== oracle.expected[i])) throw Error("CoreLib fixture disagrees with the CLR.");
    const hash = new Uint8Array(await crypto.subtle.digest("SHA-256", bytes));
    return { cases: actual.length, bytes: size,
        sha256: Array.from(hash, b => b.toString(16).padStart(2, "0")).join("") };
}
