# Stage 82 · Static fields and instance layouts

Run `npm run demo -- stage-82-static-field-layout` and `npm test`.
The fixture builds two .NET Standard DLLs. The base and derived classes
declare static fields among their instance fields. The compiler excludes
static storage from their Wasm GC layouts, including inherited field offsets.
The generated code agrees with the CLR for individual field reads and writes
through the base and derived types.

The tests also require diagnostics for static reads and writes and for a
reached type initializer. Those features are tracked in [#170](https://github.com/SamG-Coder/StandardWasm/issues/170).
