# Stage 284 · Exact readonly SZ-array interface assignment

An SZ array of a supported element type keeps its identity when passed as its exact CoreLib `IReadOnlyList<T>` or `IReadOnlyCollection<T>` interface. The compiler shares the exact nominal element proof with the existing mutable list assignment. Variant conversions and unsupported array-supplied interface methods retain located diagnostics.

Run `npm run demo -- stage-284-readonly-szarray-assignment` to compare the same fixture library on Windows x86 CLR, Node and Edge. Set `STANDARDWASM_COMPILER_PATH` to select a frozen compiler and `STANDARDWASM_MONO_CORELIB` to verify the browser Mono metadata.
