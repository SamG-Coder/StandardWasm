# Stage 82 · Static fields and instance layouts

Run `npm run demo -- stage-82-static-field-layout` and `npm test`.
The fixture builds two .NET Standard DLLs. The base and derived classes
declare static fields among their instance fields. The compiler excludes
static storage from their Wasm GC layouts, including inherited field offsets.
The generated code agrees with the CLR for individual field reads and writes
through the base and derived types.

The tests also cover linked static storage from [Stage 106](../stage-106-static-field-storage/README.md)
and construct a class whose precise type initializer runs first
([Stage 129](../stage-129-type-initializers/README.md)).
