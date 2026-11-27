# Stage 292 · Nested value fields in constructors

This .NET Standard fixture compares Windows x86 CLR, Node and Edge on direct stores through a nested value field in its outer constructor. Each store updates the nested value's actual layout, then replaces just that field of the outer value. The constructor can write a readonly field if the address feeds only that proven store. Earlier value copies and managed sibling fields retain their own contents.

```text
npm run demo -- stage-292-ctor-nested-value-fields
```

The expected results are 101, 103, 107, 109, 113 and 127. They cover shared 32-bit and 64-bit overlay views, default bytes, tag identity, distinct snapshots, a defensive copy outside the constructor, and conditional writes to a sequential child. Set `STANDARDWASM_MONO_RUNTIME` to the matching runtime pack's `runtimes/browser-wasm` directory to verify Mono metadata.

The field address must come from the same constructor's receiver, the child store must bind to an exact field definition in that image, and one adjacent scalar load supplies the value. Interior entry points and escaping addresses use the ordinary managed reference rules.
