# Stage 298 · Fixed string concatenation

The portable compiler links the original two-, three-, and four-string `String.Concat` overloads from CoreLib. The linked methods retain CoreLib's null and empty checks, overflow checks, fallback calls, and single-input reference identity. The compiler recognizes the verified allocation and bounded copy tail and writes UTF-16 code units into the new string's private storage before returning it.

```text
npm run demo -- stage-298-corelib-concat-construction
```

Windows x86 CLR supplies the expected results from the same .NET Standard fixture DLL that Node and Edge compile against the stock Mono browser runtime assemblies. Six cases cover independent destination offsets, embedded NULs and unpaired surrogates, empty and single-input identity, fresh results, repeated input references, and fallback from four inputs to three or two. Install the matching `Microsoft.NETCore.App.Runtime.Mono.browser-wasm` NuGet runtime pack or set `STANDARDWASM_MONO_RUNTIME` to its `runtimes/browser-wasm` directory.

Set `STAGE298_CORECLR=1` to reproduce the Windows CoreCLR dependency checkpoint on .NET 10.0.10. Linking reaches `Exception.GetMessageFromNativeResources` through the original overflow guard's `OutOfMemoryException` constructor. The stock Mono path uses a managed resource provider for that default exception message.
