# Stage 152 · Finalization root tracing

Run `npm run demo -- stage-152-finalization-root-tracing` after building the
self-hosted compiler with `npm test`. The runner compiles the same .NET Standard
2.0 DLL in Node and Microsoft Edge, executes the resulting standalone Wasm, and
compares seven cases with .NET 10. Results go to `artifacts/finalization-root-tracing/`.

The experiment uses real Wasm GC references to model registered objects and an
explicit worklist to trace ordinary roots and pending receivers. The queue
keeps a receiver and its fields available until its callback runs. The cases
exercise repeated suppression, transitive roots, repeated re-registration,
resurrection, suppression after discovery, and a pending receiver whose child
becomes eligible only after the pending receiver leaves the queue. The expected
checksums are `23, 0, 160, 0, 58, 63, 140`.

The CLR oracle independently runs three of the cases using actual CLR
finalizers. After two calls to `GC.SuppressFinalize` followed by two calls to
`GC.ReRegisterForFinalize`, the original registration is re-enabled and the
second re-registration adds an entry. Both callbacks can run before the waiter
returns. This behavior was measured on .NET 10.0.10. The other four cases
exercise the experiment's explicit discovery and drain steps.
[CoreCLR registration implementation](https://github.com/dotnet/runtime/blob/v10.0.10/src/coreclr/gc/gc.cpp#L48323-L48336)

The fixture supplies its roots and field traversal explicitly. Runtime
integration needs compiler-emitted roots for live frames, statics and host
references, and type-specific traversal covering object fields, reference
arrays and managed interior references. Scheduling callbacks and binding the
original CoreLib APIs remain part of the WinForms startup milestone.
