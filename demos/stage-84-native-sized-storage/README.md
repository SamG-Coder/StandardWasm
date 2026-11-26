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

Native arrays, external calling conventions and executing CoreLib delegates
need separate work. Native arithmetic and primitive pointer dereference arrived
in [Stage 132](../stage-132-unmanaged-pointers/README.md), and the old
`RejectNativeAdd` kernel now compares its wasm32 sum.
See the [browser native integer decision](../../docs/decisions/0027-native-integer-width.md).
