# Stage 60 · Class signatures across DLLs

Run `npm run demo -- stage-60-nominal-linking`. Calling `Run(5, 3)`
returns `325`. The two dependency DLLs both define
`NominalTypes.Cell` with different layouts. A bridge overload is selected
by the type's owning assembly as well as its namespace and name.

The suite compares the same three .NET Standard DLLs on the CLR,
Node and Edge. It verifies class-typed locals, arguments and return values,
identity and mutation across a transitive call, typed null propagation,
byte equality under dependency reordering, and missing or ambiguous
type assembly diagnostics.

This stage resolves public top-level classes from the bundle's supported
assembly identities. Constructors, fields and instance calls still use
their existing same-assembly rules.
