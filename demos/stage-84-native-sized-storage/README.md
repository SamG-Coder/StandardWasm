# Stage 84 · Native-sized storage

Run `npm run demo -- stage-84-native-sized-storage` and `npm test`.
The two .NET Standard DLLs round-trip signed and unsigned native-sized fields
through an unchanged dependency. The browser target uses wasm32, so the compiler
emits i32 cells and preserves the native integer stack category. Sign and zero
extension are selected by the IL conversion. The linker matches signed and
unsigned signature elements separately.

The full suite uses the original compiled DLLs for the CLR oracle. Its 64-bit
host normalizes inputs to the wasm32 range before asking the CLR for results.
For an independent width check, run the built verifier with a 32-bit .NET 10
host and `--native-storage-oracle <output.json>`. This runs the original DLLs
with the full-width inputs on the 32-bit CLR.

Native arithmetic, native arrays, pointer dereference, external calling
conventions and executing CoreLib delegates need separate work.
See [ECMA-335, Partition III](https://www.ecma-international.org/wp-content/uploads/ECMA-335_6th_edition_june_2012.pdf).
