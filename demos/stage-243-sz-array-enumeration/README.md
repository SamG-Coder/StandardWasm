# Stage 243 · Generic SZ-array enumeration

A single-dimensional, zero-based vector of an exact managed class type can
dispatch the platform `IEnumerable<T>.GetEnumerator` slot. The compiler binds
the original CoreCLR `SZArrayHelper.GetEnumerator<T>` or Mono
`Array.InternalArray__IEnumerable_GetEnumerator<T>` body. Those methods use the
existing Wasm vector reference and the original CoreLib enumerator class. Its
constructor, empty static instance, index, `Current`, `MoveNext` and `Dispose`
are all managed code from the selected runtime.

The same .NET Standard library runs on Windows x86 CLR, Node and Edge. It
checks a live array update and element identity, null elements, empty singleton
reuse, independent enumerators, both invalid `Current` positions, an immediate
array receiver, ordinary classes and arrays reaching the same interface call,
and a typed null receiver. A covariant target retains its compiler diagnostic.

Run the stage using the generated compiler.

```sh
npm run demo -- stage-243-sz-array-enumeration
```

The runner uses the Windows CoreLib by default. Set `STANDARDWASM_MONO_CORELIB`
to the exact CoreLib PE or Webcil image from a browser publish to verify that
runtime's factory against the same Windows CLR oracle. Evidence is written to
`artifacts/stage-243-sz-array-enumeration/results.json`.
