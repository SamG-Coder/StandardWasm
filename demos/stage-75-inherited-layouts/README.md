# Stage 75 · Inherited layouts across DLLs

Run `npm run demo -- stage-75-inherited-layouts` and `npm test`.
A real base, intermediate class and leaf are built into three separate
.NET Standard assemblies. The compiler emits a Wasm GC subtype chain with
base fields as a layout prefix. The base and protected base constructors
initialize the same receiver. Hidden field names retain separate storage.

The suite compares direct calls and fields through base and leaf types,
reference identity, compatible casts, dependency reordering and failure
diagnostics against the CLR. Virtual dispatch, interfaces and combining
different nonnull reference types at stack joins remain separate work.
This is a prerequisite for upstream WinForms, not a WinForms implementation.

The Wasm subtype encoding follows the [WebAssembly specification](https://webassembly.github.io/spec/core/binary/types.html).
