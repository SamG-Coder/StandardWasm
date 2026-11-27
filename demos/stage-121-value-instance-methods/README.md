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

Read-only calls pass the location's current immutable aggregate. Supported
mutating calls keep the managed receiver location so `Store` (a field
write) and `Reset` (`this = default`) update the caller's value. The
cross-assembly `Replace` case (`this = new Counter(value)`) still returns
`-2` at the unsupported write in the dependency.

See [decision 0049](../../docs/decisions/0049-read-only-value-methods.md).
