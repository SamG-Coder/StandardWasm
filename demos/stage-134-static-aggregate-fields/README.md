# Stage 134 · Static aggregate fields

The compiler reads and writes value-typed static fields in ordinary linked
.NET Standard libraries. The fixture checks the default value, an init-only
field populated by the type initializer, local and cross-assembly fields,
nested values, and copies that survive later writes.

Build the portable compiler, then run
`npm run demo -- stage-134-static-aggregate-fields`. Node and Edge compare
18 calls in sequence against the CLR running the same DLLs. Each engine
uses a single module instance throughout the sequence.
