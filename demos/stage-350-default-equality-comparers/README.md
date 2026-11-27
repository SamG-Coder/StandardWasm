# Stage 350 · Default equality comparer activation

An ordinary .NET Standard assembly exercises the original CoreCLR default comparers for strings, an equatable struct, its nullable form, an enum and a reference type that overrides `Object.Equals`. The cases cover equality, hashing, nulls and stable default instance identity. The x86 CLR oracle also verifies that the real runtime helper creates the selected generic comparer using either an open definition or a closed template with a replacement argument.

```sh
npm run demo -- stage-350-default-equality-comparers --no-rebuild
```

This stage tracks a compile checkpoint. Node and Edge must report the unsupported `QCallTypeHandle` construction in the original `RuntimeTypeHandle.CreateInstanceForAnotherGenericParameter` body for all six exports. The CLR oracle checks the actual instruction before supplying its metadata location. Even the string case reaches this frontier during static linking, although CLR execution chooses the nongeneric string comparer. Replace the expected diagnostics with CLR outcome assertions when activation and comparer execution are implemented. Results are written to `artifacts/stage-350-default-equality-comparers/`.
