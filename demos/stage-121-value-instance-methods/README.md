# Stage 121 · Read-only value-type instance methods

A linked .NET Standard library declares structs with property getters and
methods that only read `this`. The entry calls them on locals (`ldloca;
call`), on a default value, after a copy and an in-place field write, and from
inside a value constructor. A struct in the entry assembly covers the
same-assembly call path. The original Blazor entry needs this form for
`TaskAwaiter::GetResult()`.

Run `npm run demo -- stage-121-value-instance-methods`. The demo compiles
`LinkedChain(3, 5)` with the compiler running as Wasm and returns 61.
`npm test` compares every kernel against the CLR in Node and Edge.

The receiver is passed as the location's current immutable aggregate. That
is exact because the callee cannot write through `this`. A method that
does is diagnosed at its own write, so `Store` (a field write), `Reset`
(`this = default`) and `Replace` (`this = new Counter(value)`) each return
`-2` located in the dependency. Mutating methods, virtual and interface
methods on structs and primitive receivers remain future work.

See [decision 0049](../../docs/decisions/0049-read-only-value-methods.md).
