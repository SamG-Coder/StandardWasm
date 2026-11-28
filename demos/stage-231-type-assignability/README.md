# Stage 231 · Runtime type assignability

A verified literal receiver can test a runtime `Type` descriptor with the
platform `Type.IsAssignableFrom` signature. The emitted predicate uses
canonical type identity and the linked base and interface relations. It
accepts null as a source and returns false for it. The compiler includes
descriptors created by the `Object.GetType` helper during final emission.

This stage supports non-generic nominal reference targets and linked
nominal reference sources, including closed generic sources. Array,
value-type, open-generic and generic-target comparisons remain diagnosed.
A dynamically supplied descriptor outside the proven graph traps
explicitly. A negative interface result requires every reachable
interface map to be present.

Run with the Windows x86 runtime, Node and Edge after building the compiler.

```powershell
npm run demo -- stage-231-type-assignability
```

The same .NET Standard DLL checks interface inheritance, class bases,
nominal generic sources, identity and null. Results are written to
`artifacts/stage-231-type-assignability/results.json`.
