# Stage 245 · Strong GC handles

The compiler binds the authentic CoreCLR `GCHandle` runtime operations for normal handles. Each Wasm instance owns a table of managed targets. A separate occupancy marker lets an allocated handle have a null target. Tokens are even and increase for the life of the instance, and freeing a handle clears its target and occupancy. The explicit finalizer pass traces live targets in this table.

The public CoreCLR normal paths retain their managed argument checks, token conversions, target access, and release of the handle word through `Interlocked.Exchange`. Requests for weak, weak-tracking, or pinned handles raise a managed `NotSupportedException`. The compiler diagnoses the pinned-address path. The handle table raises the real managed invalid-operation error for an unknown or freed token. An engine table allocation failure stops the Wasm invocation.

Run `npm run demo -- stage-245-strong-gc-handles` with the generated compiler from `npm test`, or use `STANDARDWASM_COMPILER_PATH` to select a frozen compiler. The same ordinary DLL exercises the original CoreLib in Windows x86, Node, and Edge. A separate root probe exposes the compiler's explicit finalizer queue so the runner can observe the target while the handle is live and after release. Results and hashes are written to `artifacts/stage-245-strong-gc-handles/results.json`.
