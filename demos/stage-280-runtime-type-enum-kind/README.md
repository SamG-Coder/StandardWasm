# Stage 280 enum type classification

The .NET Standard fixture checks `Type.IsEnum` with real enum definitions, their underlying primitive types, CoreLib's Enum, arrays, generic class bases and an independently compiled user type also named System.Enum. Nominal TypeDelegator overrides still dispatch through the original managed override. The stage verifies type definitions and represented closed runtime types. Reflection on individual open generic parameters requires a separate descriptor representation.

Run `npm run demo -- stage-280-runtime-type-enum-kind` with Windows x86 CLR available. Set `STANDARDWASM_MONO_RUNTIME` to a matched untrimmed Microsoft browser-wasm runtime pack directory to select its CoreLib and facades. The Windows x86 CLR remains the oracle.
