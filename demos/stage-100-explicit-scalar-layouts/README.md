# Stage 100 · Explicit scalar layouts

A linked .NET Standard library declares explicit field offsets. The generated Wasm reads and updates the same backing bytes through each original field. A copy retains its previous bytes when another copy changes.

Run `npm run demo -- stage-100-explicit-scalar-layouts`. The fixture writes -2 through a signed 16-bit field at byte offset 1 and reads back 318766712 (0x12fffe78) through an overlapping 32-bit field. The full suite covers signed and unsigned fields, cross-word writes, and floating-point bit patterns.

Scalar overlaps contained within eight bytes share a packed cell. Default-sized explicit layouts can also use several separate scalar regions and traced reference slots. Overlapping numeric fields share a region of up to eight bytes. Coincident managed references share a traced slot, and a typed load checks the reference's Wasm representation. The fixtures exercise both alias directions, default values, disjoint fields, and independent copies. Wider connected scalar overlaps, mixed reference/scalar overlaps, nominal interface reads from a shared reference slot, unsupported declared extents, and escaping managed addresses remain diagnostics.
