# Stage 115 · Sequential reference layouts

A linked .NET Standard library declares `[StructLayout(LayoutKind.Sequential)]`
classes. The original browser-wasm CoreLib declares `System.Delegate` and
`System.Type` in the same form. The generated Wasm constructs the classes and
uses abstract dispatch, a base-calling override, and narrow, 64-bit, embedded
value, reference and static fields across the link.

Run `npm run demo -- stage-115-sequential-reference-layouts`. The demo
compiles `LedgerFields(3, 5)` with the compiler running as Wasm and returns
61274. `npm test` compares every kernel against the CLR in Node and Edge.

Sequential classes keep the nominal managed representation of auto classes.
Field order only matters to byte views. Those are interop marshalling,
`Marshal.SizeOf`, overlays and raw field addresses, and they remain diagnosed.
The compiler reports `-2` at the `newobj` for these shapes:

- an explicit-layout class
- a sequential class with `Pack` or `Size`
- a sequential class derived from an auto class other than `Object`. CoreCLR
  throws `TypeLoadException` for this shape outside the platform CoreLib.

A sequential class with a precise static constructor now compiles. Its
initializer runs at construction ([Stage 129](../stage-129-type-initializers/README.md)).

See [decision 0045](../../docs/decisions/0045-sequential-reference-layouts.md).

The verifier also checks that the compiler diagnoses a sequential COM-imported class whose original DLL the CLR rejects.
