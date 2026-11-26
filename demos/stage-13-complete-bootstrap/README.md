# Stage 13 · Complete compiler bootstrap

Run the following from the repository root after npm ci.

    npm run demo -- stage-13-complete-bootstrap
    npm test

The demo compiles BootstrapKernel from BootstrapLibrary.dll and returns 43 for input 21. The test suite builds a full compiler from that same DLL.

1. The initial Wasm module compiles seven named entry points and their reachable methods into one module.
2. The first and second generated modules use their own CreateBytes, SetByte, ReadByte and CompileModule exports to compile the same DLL. Each output must match the reference module produced on the CLR byte for byte.
3. The second generated module runs every portable generated-code case through its own Compile, CompileModule, CompileBundle and buffer exports. The harness checks that each generated compiler has zero imports and moves the DLL and manifest as plain bytes between generations.

The Node test writes the verified final module to artifacts/StandardWasm.Compiler.wasm. The manifest accepted by CompileModule consists of consecutive descriptors with a little-endian u32 MethodDef row, little-endian u32 byte length, and printable ASCII export name bytes. Export aliases share a function. The compiler diagnoses duplicate names, invalid rows, and truncated records before touching the caller's output.

The generated compiler supports the IL subset documented by stages 09 through 12 and 15. The older CLI and legacy tests still exercise additional features. The WinForms and WebGPU demo is verified separately with npm run test:winforms.
