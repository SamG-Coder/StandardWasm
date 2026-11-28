# Stage 251 · CoreLib string interface tests

Dynamic reference tests and casts use the supplied CoreLib's interface declarations to identify the nongeneric interfaces implemented by `System.String`. The compiler checks the canonical Wasm string heap and preserves the original reference on success. Class references, primitive boxes, null and managed cast failures use the existing paths. Tests that would also require unsupported dedicated representations retain their diagnostic.

Run `npm run demo -- stage-251-string-interface-type-tests`. Set `STANDARDWASM_COMPILER_PATH` to verify a frozen compiler. The same C# library runs on Windows x86 CLR, Node and Edge, and the runner checks that the overlapping String and Array interface still produces a located compile diagnostic. Evidence goes to `artifacts/stage-251-string-interface-type-tests/results.json`.
