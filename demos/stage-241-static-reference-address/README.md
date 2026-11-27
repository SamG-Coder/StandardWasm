# Stage 241 · Closed generic static reference addresses

Run `npm run demo -- stage-241-static-reference-address` with a built portable compiler. The same .NET Standard DLL runs against the Windows x86 CLR, Node and Edge. The fixture checks direct reads and writes, a managed byref mutation, a volatile compare-exchange, distinct static initialization in two closed types, and a scalar instantiation sharing the same FieldDef.

The compiler records reference-cell demand by metadata identity and still allocates a distinct global per closed type. A scalar or native field of the same generic definition keeps its original representation. The addressed scalar case keeps the existing unsupported diagnostic.
