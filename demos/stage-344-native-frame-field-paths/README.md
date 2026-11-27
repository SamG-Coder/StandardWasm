# Stage 344 · Native addresses of nested local fields

The compiler verifies the declaring type and native offset of every field in a local address path. An immediately converted address then uses a single linear-memory slot owned by the frame. Matching immediate indirect reads, whole-value copies, and clearing the local observe the same bytes. Existing diagnostics remain for managed byref uses of a promoted local that lack an adapter.

The fixture checks a native character write followed by a managed read, the independence of a snapshot after the original clears, the offsets of nested fields in two separate locals, and distinct addresses without dereferencing them. It also verifies the located diagnostic for sharing such a local with a managed byref call that has no native adapter.

```sh
npm run demo -- stage-344-native-frame-field-paths --no-rebuild
```

Use the generated compiler or set `STANDARDWASM_COMPILER_PATH`. Windows x86 CLR, Node and Edge should return 65334, 6500, 1, and 1 for the supported cases. The mixed byref case returns 202 on the CLR and produces a located compiler diagnostic in Node and Edge. The runner records results in `artifacts/stage-344-native-frame-field-paths/results.json`.
