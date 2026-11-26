# Stage 57 · Linked exception regions

Run `npm run demo -- stage-57-linked-exceptions`. Calling `Run(18, 0, 4)`
returns `116`. The leaf DLL rethrows divide-by-zero, the intermediate
DLL updates its trace in cleanup before catching it, and the entry DLL
runs its own cleanup.

The three projects produce separate .NET Standard assemblies. The suite
compares the same DLLs on the CLR, Node and Edge. It checks byte equality
when the bundle dependencies are reordered and verifies that an invalid
cleanup diagnostic identifies its owning DLL, method row and IL offset.

The linked dependency surface currently supports static scalar
signatures. The compiler's internal Wasm exception references do not
change those signatures.
