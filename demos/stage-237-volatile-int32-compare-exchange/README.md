# Stage 237 · Compare-exchange on volatile Int32 fields

Run `npm run demo -- stage-237-volatile-int32-compare-exchange` after building the portable compiler. The same compiled .NET Standard DLL compares Windows x86 CLR, Node and Edge results for successful replacement, a failed comparison, separate class instances, a typed null reference, static and array locations, and the existing Exchange overload. The expected values are 901, 907, 911, 919 and 929.

The compiler recognizes the exact trusted CoreLib Int32 CompareExchange method and uses the existing projected managed location for a class field carrying the authenticated IsVolatile modifier. The comparison reads the previous value once and writes only on equality. The read, comparison and optional write run synchronously inside a single Wasm agent with no shared guest storage or host callback.
