# Stage 101 · Nested overlay views

An ordinary .NET Standard library defines three nested views of the same explicit layout. The compiler unpacks views from shared bytes and packs value assignments back into the union. A six-byte view begins at an unaligned offset. Writes preserve the two bytes outside that view.

Run `npm run demo -- stage-101-nested-overlay-views`. It returns 5 after copying an overlay and overwriting the original. The full suite compares shared reads, nested writes, default values, and signed boundaries with the CLR.

Nested views can contain dense, naturally aligned scalar fields in a sequential layout, or same-width scalar fields in an automatic layout. The automatic layout follows the field order observed on .NET 10 CoreCLR. The compiler rejects dense extents of 3, 5, 6 or 7 bytes because CoreCLR value copies can preserve trailing padding that a field-only Wasm aggregate cannot represent. Larger, padded, reference-containing, nested explicit and mixed-width automatic views still require layout support.

The separate CoreLib check recompiles an ordinary .NET 10 overlay against the installed CoreLib and compares raw DateTime bit copies in Node and Edge with the CLR. It also compares an inline-array value copy against the CLR. Run `node demos/stage-101-nested-overlay-views/verify-corelib.mjs` after building the self-hosted compiler.
