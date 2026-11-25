# Stage 13 · Complete compiler bootstrap

Run the following from the repository root after npm ci.

    npm run demo -- stage-13-complete-bootstrap
    npm test

The demo compiles BootstrapKernel from BootstrapLibrary.dll and returns 43 for input 21. The test suite builds a full compiler from that same DLL.

1. The initial Wasm module compiles six named entry points and their reachable methods into one module.
2. The new module uses its own CreateBytes, SetByte, ReadByte and CompileModule exports to compile the same DLL again. The second module must match every byte of the first.
3. The second module runs every portable generated-code case through its own Compile, CompileModule and buffer exports. The harness checks that each generated compiler has zero imports. No managed references from the initial module cross into the later stages.

The Node test writes the verified final module to artifacts/StandardWasm.Compiler.wasm. The manifest accepted by CompileModule consists of consecutive descriptors with a little-endian u32 MethodDef row, little-endian u32 byte length, and printable ASCII export name bytes. Export aliases share a function. The compiler diagnoses duplicate names, invalid rows, and truncated records before touching the caller's output.

The generated compiler supports the IL subset documented by stages 09 through 12. The older CLI and legacy tests still exercise additional features. The WinForms and WebGPU backend is independent of this acceptance check.
