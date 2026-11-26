# Stage 44 · Portable checked integer arithmetic

Run `npm run demo -- stage-44-checked-integers`. The portable compiler reads the .NET Standard library and emits a Wasm export. `MulI32(-12, 7)` returns `-84`.

The library exercises checked addition, subtraction, and multiplication with signed and unsigned 32-bit and 64-bit operands. The compiler checks the width of the result before publishing it. Overflow raises `System.OverflowException` on the CLR and causes a Wasm trap in the generated export.

`npm test` compares the same DLL on the CLR, Node and Chromium using integer boundaries, deterministic mixed values, and a call into a separately built dependency. Stage 45 covers checked casts. Managed exception handlers remain open.
