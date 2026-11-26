# Stage 59 · Assembly-scoped object layouts

Run `npm run demo -- stage-59-linked-objects`. Calling `Run(5)` returns
`47` after all three assemblies use their own types and fields.

The compiler keeps type and field identities separate when different
DLLs reuse metadata row numbers. The fixtures cover different field
types at the same row, mutually recursive fields, instance calls
inside dependencies, and typed null propagation. A `System.Object`
argument and result can carry an opaque object reference through two
dependency DLLs and back to its caller.

Run `npm test` for CLR, Node and Edge comparisons, reordered bundle
byte equality and generated-compiler replay. External signatures that
name a specific class still require nominal type resolution.
