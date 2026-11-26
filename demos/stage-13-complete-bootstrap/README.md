# Stage 13 · Complete compiler bootstrap

Run the following from the repository root after npm ci.

    npm run demo -- stage-13-complete-bootstrap
    npm test

The demo compiles BootstrapKernel from BootstrapLibrary.dll and returns 43 for input 21. The test suite builds a full compiler from that same DLL.

1. The initial Wasm module compiles seven named entry points and their reachable methods into one module.
2. The first and second generated modules use their own CreateBytes, SetByte, ReadByte and CompileModule exports to compile the same DLL. Each output must match the reference module produced on the CLR byte for byte.
3. The second generated module runs every portable generated-code case through its own Compile, CompileModule, CompileBundle and buffer exports. The harness checks that each generated compiler has zero imports and moves the DLL and manifest as plain bytes between generations.

The Node test writes the verified final module to artifacts/StandardWasm.Compiler.wasm. The manifest accepted by CompileModule consists of consecutive descriptors with a little-endian u32 MethodDef row, little-endian u32 byte length, and printable ASCII export name bytes. Export aliases share a function. The compiler diagnoses duplicate names, invalid rows, and truncated records before touching the caller's output.

The [stage 27 diagnostics API](../stage-27-portable-diagnostics/README.md) is also available from the generated compiler. The bootstrap suite verifies those exports and replays their failure and success cases.

The generated compiler supports the IL subset documented by stages 09 through 12 and 15. The older CLI and legacy tests still exercise additional features. The WinForms and WebGPU demo is verified separately with npm run test:winforms.

## Audit library coverage

After running `npm test`, run:

    npm run audit:portable
    npm run audit:portable -- --require-complete

The audit loads the saved second-generation compiler and submits the managed DLL and exact public static MethodDef row for each direct CLR oracle method. Methods are compiled separately so one unsupported export does not hide the rest of its assembly. Node and Edge run the original oracle inputs against the generated Wasm. Fixture row selection uses CLR metadata inspection. All audited IL compilation runs inside the supplied Wasm compiler.

The report at `artifacts/portable-audit.json` records the compiler and corpus hashes, pass counts, and per-method diagnostics or mismatches. The report separates matching returns, matching CLR exception types, and legacy Wasm traps. For continuity with older reports, a Wasm trap still counts toward coverage when the CLR oracle expects an exception. The strict command exits nonzero if either engine has coverage gaps. The regular `npm test` run still verifies the generated-code and bootstrap fixtures.

Use `--compiler <wasm>` and `--output <json>` to compare a second portable compiler against the same oracle.

On the 40,240-case direct CLR corpus, the Stage 13 artifact matched 3,958 returns per engine and recorded 59 legacy traps. The compiler from the [Math integration probe](https://github.com/SamG-Coder/StandardWasm/issues/83#issuecomment-7180745948) matched 40,043 returns and 197 typed exceptions per engine, with zero legacy traps. Track remaining work in the [coverage audit issue](https://github.com/SamG-Coder/StandardWasm/issues/81).
