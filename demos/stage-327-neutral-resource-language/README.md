# Stage 327 · Neutral resource language

The linker recognizes the genuine CoreLib `GetCustomAttribute<NeutralResourcesLanguageAttribute>(Assembly)` overload. It reads `AssemblyDef` attribute metadata for each bundled assembly, resolves the constructor's declaring type through assembly identity and forwarding, and emits a call to the original one-string CoreLib constructor. CoreLib still supplies the fallback location and the managed property getters.

The .NET Standard fixture checks the declared culture and fallback, missing attributes on another assembly, fresh instances, and the typed null-assembly exception. It also combines this query and the existing plural `AssemblyMetadataAttribute` query in one Wasm module.

```sh
npm run demo -- stage-327-neutral-resource-language --no-rebuild
```

Use an existing generated compiler or set `STANDARDWASM_COMPILER_PATH`. The five cases should each return `1` on Windows x86 CLR, Node and Edge. The runner writes the compiler and fixture hashes to `artifacts/stage-327-neutral-resource-language/results.json`.

This stage covers the one-string constructor used by the unchanged WinForms framework assemblies. The constructor with an explicit fallback location and malformed duplicate rows still produce diagnostics. The original WinForms replay also exercises the .NET 9 `System.Runtime` reference from its `System.Text.Encodings.Web` dependency against the selected .NET 10 facade. [Framework facade version policy](../../docs/decisions/0038-framework-facade-version.md)
