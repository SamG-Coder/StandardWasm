# Stage 100 · Explicit scalar layouts

A linked .NET Standard library declares explicit field offsets. The generated Wasm reads and updates the same backing bytes through each original field. A copy retains its previous bytes when another copy changes.

Run `npm run demo -- stage-100-explicit-scalar-layouts`. The fixture writes -2 through a signed 16-bit field at byte offset 1 and reads back 318766712 (0x12fffe78) through an overlapping 32-bit field. The full suite covers signed and unsigned fields, cross-word writes, and floating-point bit patterns.

The compiler supports scalar fields contained within eight bytes. Larger explicit types, nested value fields, managed reference fields, and escaping managed addresses require separate representation support. This stage implements managed field access and copies.
