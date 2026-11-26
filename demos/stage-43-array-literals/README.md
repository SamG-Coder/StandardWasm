# Stage 43 · Primitive array literals

Run the demo with `npm run demo -- stage-43-array-literals`. The portable compiler reads the .NET Standard library and emits a Wasm export. `IntegerAt(1)` returns `-93`.

Roslyn emits a call to `RuntimeHelpers.InitializeArray` for these primitive array literals. The compiler verifies the framework method, the local field and the file-backed data range. The generated Wasm allocates a fresh typed GC array from a passive data segment. Other field handle uses are rejected.

`npm test` compares the library on the CLR, Node and Chromium. It checks element widths, IEEE values, independent allocations, UTF-16 and odd-sized byte data in the same module, out-of-bounds reads, an invalid field RVA, and a branch into the middle of the initializer. Array bounds violations surface as Wasm traps.
