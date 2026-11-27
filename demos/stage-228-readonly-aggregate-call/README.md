# Stage 228 · Readonly aggregate arguments

A readonly class field can pass its existing managed cell as the final
argument of an exact same-module call. The callee's closed signature must
take the same value type by reference. Every load of that parameter must
feed an immediate field read, with no alternate entry into the read.
Taking the parameter's address, reassigning it, and forwarding it are
outside this proof.

Run with the Windows x86 runtime, Node and Edge after building the compiler.

```powershell
npm run demo -- stage-228-readonly-aggregate-call
```

The same .NET Standard DLL checks a closed generic aggregate, an
exception and payload that retain their original identity, and both
field-owner and call-receiver null checks. The compiler diagnoses a
forwarded address and a write through an address taken from an `in`
parameter. Results are written to
`artifacts/stage-228-readonly-aggregate-call/results.json`.
