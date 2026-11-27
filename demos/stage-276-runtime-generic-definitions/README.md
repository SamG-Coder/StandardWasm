# Stage 276 runtime generic type definitions

The .NET Standard fixture checks canonical open and closed generic definitions, captured outer parameters, runtime factory output, repeated identity, fresh typed failures for nongeneric types and arrays, null receivers and a genuine nominal virtual override. InvalidOperationException uses the linked CoreLib message and constructor.

Run `npm run demo -- stage-276-runtime-generic-definitions` with Windows x86 CLR available. Set `STANDARDWASM_MONO_RUNTIME` to a matched untrimmed browser-wasm runtime pack directory to verify its CoreLib and facades. The fixture remains the same and the Windows x86 CLR supplies the oracle.
