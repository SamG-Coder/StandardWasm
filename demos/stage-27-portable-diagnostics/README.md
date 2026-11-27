# Stage 27 · Portable compiler diagnostics

Run from the repository root.

    npm run demo -- stage-27-portable-diagnostics

The Wasm compiler reads DiagnosticLibrary.dll and attempts to compile BoxedEnum. The command prints assembly 0, the method's MethodDef row, and IL_0001. Compilation returns -2 because the method boxes a nominal enum. The demo resolves the row from the DLL and verifies that rejection preserves every output byte.

The self-hosted compiler exposes CompileDetailed, CompileModuleDetailed and CompileBundleDetailed. Each takes its status-only counterpart's arguments followed by a diagnostic array made with CreateDiagnostic. Use ReadDiagnostic(location, index) to read the fields from JavaScript. The return value and output-byte contract are the same as for the status-only exports.

| Slot | Meaning |
| --- | --- |
| 0 | Zero-based bundle position. The primary DLL is 0. An unknown assembly is -1. |
| 1 | One-based MethodDef row in that DLL. An unknown method is 0. |
| 2 | Zero-based byte offset from the start of the method's IL. Method-wide and metadata failures use -1. |

An instruction failure reports the method being decoded or analyzed. A failed call resolution reports the caller's call instruction. If the compiler reaches an unsupported instruction in a separate dependency, its location identifies that dependency and its own MethodDef row. Successful compilation and failures without an attributable location produce [-1, 0, -1]. An undersized diagnostic array returns -3 before writing to either buffer.

Run npm test for the CLR, Node and Edge comparisons. The suite checks the offsets against independent IL decoding, compares failure statuses and unchanged output buffers, and verifies byte-identical success with and without diagnostics. The regenerated compiler exposes and replays all three detailed entry points. The location record identifies methods by metadata row. It does not include source file or line information.
