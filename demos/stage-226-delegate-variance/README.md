# Stage 226 · Delegate reference variance

Run `npm run demo -- stage-226-delegate-variance` with the Windows x86
CLR, Node and Edge. The same .NET Standard assembly exercises covariant
results, contravariant arguments, both together, and preservation of
null and delegate reference identity.

The verifier retains the nominal `Func` and `Action` generic arguments.
Variant assignments keep the original closure. General delegate thunks
carry reference arguments and results as nullable Wasm references of
the same physical type. The thunk input and the Invoke result restore
the types expected by ordinary managed functions. Value positions
retain their existing representation and require identical nominal
arguments.

The fixture covers implicit variance and invocation. Dynamic delegate
casts and invocation lists use the separate multicast representation.
