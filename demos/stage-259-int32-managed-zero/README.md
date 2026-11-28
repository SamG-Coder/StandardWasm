# Stage 259 · Zero initialization through managed Int32 locations

A closed generic `initobj` uses the existing managed location for `Int32` and `UInt32`. It writes numeric zero to the original destination, including array elements and projected fields. The managed location still handles the null check and preserves unrelated values.

Run `npm run demo -- stage-259-int32-managed-zero`. Set `STANDARDWASM_COMPILER_PATH` to verify a frozen compiler. The same library runs in Windows x86 CLR, Node and Edge. It covers signed and unsigned locals, array, class-field and aggregate-field aliases, typed null failure and existing Boolean and reference defaults. The runner writes `artifacts/stage-259-int32-managed-zero/results.json`.
