# Stage 100 · Explicit scalar layouts

A linked .NET Standard library declares explicit field offsets. The generated Wasm reads and updates the same backing bytes through each original field. A copy retains its previous bytes when another copy changes.

Run `npm run demo -- stage-100-explicit-scalar-layouts`. The fixture writes -2 through a signed 16-bit field at byte offset 1 and reads back 318766712 (0x12fffe78) through an overlapping 32-bit field. The full suite covers signed and unsigned fields, cross-word writes, and floating-point bit patterns.

Scalar overlaps contained within eight bytes share a packed cell. For a default-sized, default-packed explicit type with fields outside that cell, the compiler also supports disjoint scalars and aligned string fields. Identical-offset CLI `int32` fields share a single typed slot. The fixture checks alias writes in both directions, default values, and independent copies containing a traced string. Partial overlaps in this wider layout, declared sizes, nested overlays, and escaping managed addresses remain diagnostics.
