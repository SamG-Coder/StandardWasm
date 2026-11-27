# Stage 273 runtime generic type classification

The same .NET Standard assembly checks `Type.IsGenericType` on the Windows x86 CLR, Node and Edge. It covers plain types, open definitions, concrete constructed types, arrays of generic elements, runtime type factories, null receivers and ordinary virtual overrides on `TypeDelegator`.

Run `npm run demo -- stage-273-runtime-type-generic-kind` with the Windows x86 runtime available.
