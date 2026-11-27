# Stage 210 — Unboxing a closed generic reference

An ordinary .NET Standard assembly uses `unbox.any !!0` for casts to closed
generic reference types. StandardWasm resolves the closed type and uses the
existing checked reference cast. Casting to `object` preserves the original
reference directly.

After building the compiler, run on Windows with the x86 CLR and Edge.

```powershell
npm run demo -- stage-210-generic-reference-unbox
```

Windows x86, Node and Edge should agree on eight results covering class and
interface identity, assignment through a generic out parameter, null,
string and object identity, a typed `InvalidCastException` and primitive
unboxing. The verifier writes its report to
`artifacts/stage-210-generic-reference-unbox/results.json`.
