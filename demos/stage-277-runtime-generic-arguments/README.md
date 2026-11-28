# Stage 277 · Runtime generic type arguments

This fixture compiles the same .NET Standard assembly against the selected runtime's real metadata and verifies calls to `System.Type.GetGenericArguments` in Node and Edge. The default oracle runs on Windows x86 CLR.

```text
npm run demo -- stage-277-runtime-generic-arguments
```

Expected results in fixture order are 101, 103, 107, 109, 1, 113, 127, 131 and 137 on CoreCLR. Set `STANDARDWASM_MONO_RUNTIME` to a matching Mono browser runtime pack's `runtimes/browser-wasm` directory to check Mono metadata. The fifth case returns 0 on stock Browser Mono 10.0.10 because it examines the array type itself. CoreCLR examines the element type.

The fixture covers canonical type identities, nested argument order, independent mutable nonempty arrays, sharing across empty queries, typed null and bounds exceptions, and a user-defined virtual override. The compiler path covers linked closed descriptors and supported array shapes. Open generic parameter results, multidimensional arrays, pointers and byref reflection are outside this stage.
