# Stage 116 · Native integer vectors

Run `npm run demo -- stage-116-native-integer-vectors`. The signed example
returns -42. Use `npm test` for the CLR, Node and Edge comparisons.

The fixture uses ordinary .NET Standard 2.0 assemblies with native-integer
array signatures, fields, allocation, loads and stores. It checks signed and
unsigned wasm32 values, zero initialization, linked field aliasing, null,
negative length and invalid index behavior.

The CLI native vector type uses a dedicated Wasm GC array of wasm32 cells.
The CLR permits signed and unsigned native vectors to share an array type
for assignment and array operations. Fixed-width integer vectors have a
separate Wasm heap type. Native element addresses and architecture-dependent
RVA initialization remain outside this stage.
