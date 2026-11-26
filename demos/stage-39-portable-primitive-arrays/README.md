# Stage 39 · Portable primitive array storage

Run from the repository root.

    npm run demo -- stage-39-portable-primitive-arrays
    npm test

The compiler reads a .NET Standard DLL and emits a module whose signed 16-bit array returns 32767 after storing -32769. Array signatures and fields preserve the storage width. Packed reads use the sign extension or zero extension encoded in IL. Floating arrays store f32 or f64 values directly and initialize new cells to positive zero.

The CLR oracle checks narrow integer wrapping, unsigned comparisons, boolean arrays, array lengths, IEEE values and bounds failures. The test module passes floating arrays through fields and typed method arguments. The self-recompiled compiler replays these cases. Negative lengths and out-of-range indices trap in Wasm and throw on the CLR. An allocation that names a platform primitive with a different AssemblyRef token is rejected before output is written.

Small scalar signature normalization, general managed addresses and constant array initializers remain outside this stage.
