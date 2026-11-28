# Stage 287 · Runtime value type classification

This .NET Standard fixture compares calls to `Type.IsValueType` against Windows x86 CLR in Node and Edge. The compiler verifies the genuine nonvirtual CoreLib getter and its protected virtual call. Canonical descriptors classify the linked type using metadata identity. Ordinary `Type` subclasses execute the original getter, including their own `IsValueTypeImpl` overrides.

```text
npm run demo -- stage-287-runtime-value-type-kind
```

Expected results are 101, 103, 107, 109, 113, 127, and 131. The cases include primitive types, open and closed structs, nullable values, enum types and their reference-type bases, arrays, typed null and nominal overrides. Set `STANDARDWASM_MONO_RUNTIME` to the matching runtime pack's `runtimes/browser-wasm` directory to check the same cases with Mono metadata. Individual generic-parameter descriptors are outside this stage.
