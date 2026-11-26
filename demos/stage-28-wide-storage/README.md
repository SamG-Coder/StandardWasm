# Stage 28 · Managed 64-bit storage

Run from the repository root.

    npm run demo -- stage-28-wide-storage

The compiler running in Wasm compiles Ledger.Apply from WideStorageLibrary.dll. Apply takes 9007199254740993, stores a derived value through a helper in a long array held by a sealed object, then reads the field and array back. The result is 36028797018963972.

The compiler uses mutable Wasm GC i64 array elements for long and ulong arrays, and i64 struct fields for long and ulong fields on supported sealed classes. Sealed objects can also retain these arrays in reference fields. Array length and indexing remain i32. The same i64 bits support signed or unsigned arithmetic according to the IL operation after a load. Arrays passed to helper methods retain their reference identity and mutations.

Run npm test to compare exact BigInt results with the CLR in Node and Edge. The fixtures check high-bit values, default-zero fields and elements, zero-length arrays, index and allocation failures, and access through a default-null array field. CLR exceptions on these failures surface as Wasm traps. Managed references to array elements are still unsupported. The verifier checks that the compiler reports the ldelema IL offset without writing output bytes. The generated compiler self-replay reruns the same cases.
