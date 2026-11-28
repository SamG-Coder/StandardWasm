# Stage 219 — Exact TypeHandle round-trips

For a contiguous `typeof(T).TypeHandle` expression, the compiler can prove
the receiver is a non-null canonical runtime type descriptor. The exact
CoreLib virtual getter changes the CLI stack category from `Type` to
`RuntimeTypeHandle`. Both categories use the existing descriptor in WebAssembly.

After building the compiler, run on Windows with x86 .NET and Edge.

```powershell
npm run demo -- stage-219-exact-type-handle
```

The fixture exercises nominal and closed generic types, generic method
arguments, and ordinary dispatch to an unrelated virtual property. A
getter on a receiver from a method argument or a control-flow join still
goes through ordinary virtual binding and produces a located diagnostic
where that binding is unsupported. Results are written to
`artifacts/stage-219-exact-type-handle/results.json`.
