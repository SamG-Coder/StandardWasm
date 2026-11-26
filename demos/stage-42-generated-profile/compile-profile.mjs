async function fetchBytes(file) {
    const response = await fetch(new URL(file, import.meta.url));
    if (!response.ok) throw Error("Could not load " + file + " (" + response.status + ")");
    return new Uint8Array(await response.arrayBuffer());
}

async function standalone(bytes, name) {
    const module = await WebAssembly.compile(bytes);
    if (WebAssembly.Module.imports(module).length)
        throw Error(name + " must run without Wasm runtime imports");
    return WebAssembly.instantiate(module);
}

export async function compileProfile() {
    const [compilerBytes, image, manifest] = await Promise.all([
        fetchBytes("./StandardWasm.Compiler.wasm"), fetchBytes("./Profile.dll"), fetchBytes("./Profile.exports")
    ]);
    const { exports: compiler } = await standalone(compilerBytes, "The generated compiler");

    function upload(bytes) {
        // Wasm GC arrays are opaque to JavaScript and belong to this compiler instance.
        const buffer = compiler.CreateBytes(bytes.length);
        for (let i = 0; i < bytes.length; i++) compiler.SetByte(buffer, i, bytes[i]);
        return buffer;
    }

    const capacity = 32768;
    const output = compiler.CreateBytes(capacity);
    const diagnostic = compiler.CreateDiagnostic();
    const length = compiler.CompileModuleDetailed(upload(image), upload(manifest), output, diagnostic);
    if (length <= 0 || length > capacity) {
        const row = compiler.ReadDiagnostic(diagnostic, 1);
        const offset = compiler.ReadDiagnostic(diagnostic, 2);
        throw Error("Profile compilation failed (" + length + "), MethodDef " + row + ", IL offset " + offset);
    }
    const bytes = Uint8Array.from({ length }, (_, i) => compiler.ReadByte(output, i));
    return { instance: await standalone(bytes, "The generated Profile"), bytes };
}
