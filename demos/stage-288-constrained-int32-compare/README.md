# Stage 288 · Constrained Int32 comparison

A closed `constrained.` call to the authentic CoreLib `IComparable<int>.CompareTo(int)` uses the Int32 managed receiver cell and runs the original `Int32.CompareTo(int)` method body. The compiler proves the closed interface implementation and dispatch slot from the supplied runtime metadata.

Run `npm run demo -- stage-288-constrained-int32-compare` to compare identical assemblies on Windows x86 CLR, Node and Edge. Select a frozen compiler with `STANDARDWASM_COMPILER_PATH` and the browser Mono metadata with `STANDARDWASM_MONO_CORELIB`. The fixture covers lower, equal and higher values, both directions between the signed endpoints, direct instance dispatch, and the diagnostic for the still unsupported constrained unsigned call.
