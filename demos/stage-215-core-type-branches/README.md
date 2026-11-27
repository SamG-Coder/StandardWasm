# Stage 215 — Closed type comparisons in CoreLib

The compiler resolves both type operands of a closed `typeof` comparison
before linking either arm of the branch. This stage admits the original
CoreLib `System.Type` operator MethodDefs alongside assembly references to
the same operators. The comparison checks canonical type identities.

After building the compiler, run on Windows with x86 .NET and Edge.

```powershell
npm run demo -- stage-215-core-type-branches
```

Windows x86, Node and Edge agree on seven .NET Standard cases, including
reference and value keys, equality and inequality, and comparisons of two
closed generic arguments. Results are written to
`artifacts/stage-215-core-type-branches/results.json`.

In the untouched WinForms bundle, the closed `Dictionary<Control, …>`
constructor now omits the string-specific branch. The original comparer
initialization call remains part of the reachable constructor.
