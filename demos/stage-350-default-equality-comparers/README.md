# Stage 350 · Default equality comparer activation

An ordinary .NET Standard assembly exercises the original CoreCLR default comparers for strings, an equatable struct, its nullable form, an enum and a reference type that overrides `Object.Equals`. The cases cover equality, hashing, nulls and stable default instance identity. The x86 CLR oracle also verifies that the real runtime helper creates the selected generic comparer using either an open definition or a closed template with a replacement argument.

```sh
npm run demo -- stage-350-default-equality-comparers --no-rebuild
```

The equatable struct and cached identity cases execute in Node and Edge and compare their results against the CLR. The other four cases track exact linker diagnostics in the reachable managed comparer methods. Linking currently visits alternative comparer implementations for the same closed type. Results are written to `artifacts/stage-350-default-equality-comparers/`.
