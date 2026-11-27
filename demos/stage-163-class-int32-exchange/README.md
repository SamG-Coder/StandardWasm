# Stage 163 · Class Int32 field aliases and exchange

Run `npm run demo -- stage-163-class-int32-exchange` with the .NET 10 SDK,
the x86 Windows runtime, Node and Microsoft Edge. The same fixture and
original framework DLLs are linked in both Wasm engines. Each case is
compared against the CLR.

A class field address retains the original object and the nominal field
selector. Reads, ordinary writes, and separate references returned for the
same field observe one location. The exact signed
`Interlocked.Exchange(ref int, int)` overload reads the previous value and
writes the replacement without a host call or a suspension. This applies to
traced storage owned by one Wasm agent.

The cases use one shared managed call site with class, array, stack-local,
static and projected struct fields. They check old values, alias writes,
unchanged sibling fields, null location handles, and null class receivers.
Results go to `artifacts/stage-163-class-int32-exchange/results.json`.
