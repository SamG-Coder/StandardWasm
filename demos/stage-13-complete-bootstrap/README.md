# Stage 13 · Complete compiler bootstrap

Run the following from the repository root after npm ci.

    npm run demo -- stage-13-complete-bootstrap
    npm test

The demo compiles BootstrapKernel from BootstrapLibrary.dll and returns 43 for input 21. The test suite builds a full compiler from that same DLL.

1. The initial Wasm module compiles six named entry points and their reachable methods into one module.
2. The first and second generated modules use their own CreateBytes, SetByte, ReadByte and CompileModule exports to compile the same DLL. Each output must match the reference module produced on the CLR byte for byte.
3. The second generated module runs every portable generated-code case through its own Compile, CompileModule and buffer exports. The harness checks that each generated compiler has zero imports and moves the DLL and manifest as plain bytes between generations.

The Node test writes the verified final module to artifacts/StandardWasm.Compiler.wasm. The manifest accepted by CompileModule consists of consecutive descriptors with a little-endian u32 MethodDef row, little-endian u32 byte length, and printable ASCII export name bytes. Export aliases share a function. The compiler diagnoses duplicate names, invalid rows, and truncated records before touching the caller's output.

The generated compiler supports the IL subset documented by stages 09 through 12. The older CLI and legacy tests still exercise additional features. The WinForms and WebGPU backend is independent of this acceptance check.

## Audit library coverage

After running `npm test`, run:

    npm run audit:portable
    npm run audit:portable -- --require-complete

The audit loads the saved second-generation compiler and submits the managed DLL and exact public static MethodDef row for each direct CLR oracle method. Methods are compiled separately so one unsupported export does not hide the rest of its assembly. Node and Edge run the original oracle inputs against the generated Wasm. Fixture row selection uses CLR metadata inspection. All audited IL compilation runs inside the supplied Wasm compiler.

The report at `artifacts/portable-audit.json` records the compiler and corpus hashes, pass counts, and per-method diagnostics or mismatches. A runtime trap counts as a failure match where the CLR oracle expects an exception. The strict command exits nonzero if either engine has coverage gaps. The regular `npm test` run still verifies the generated-code and bootstrap fixtures.

Use `--compiler <wasm>` and `--output <json>` to compare a second portable compiler against the same oracle.

On the same 38,309-case direct CLR corpus, the Stage 13 artifact passed 4,017 cases per engine. The compiler generated on the Stage 38 branch passed 29,656. That run left 34 unchecked floating-point to byte cases, 3,285 floating-point remainder cases, and 5,334 `System.Math` cases as compile rejections. [Coverage audit](https://github.com/SamG-Coder/StandardWasm/issues/81) · [ToByte](https://github.com/SamG-Coder/StandardWasm/issues/84) · [Remainder](https://github.com/SamG-Coder/StandardWasm/issues/82) · [Math](https://github.com/SamG-Coder/StandardWasm/issues/83)
