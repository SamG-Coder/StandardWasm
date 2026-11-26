# Stage 92 · Nominal enum fields

Run `npm run demo -- stage-92-nominal-enums` and `npm test`.
Two compiled .NET Standard assemblies exercise nominal enum field and
method signatures. The eight C# backing types use their integer CLR
stack behavior and the corresponding Wasm field storage. The fixture
checks flag masks, unnamed values, exact overload selection and packed
loads across an inherited layout.

Enum storage does not implement boxing, reflection or native ABI behavior.

See the [nominal enum storage decision](../../docs/decisions/0034-nominal-enum-storage.md).
