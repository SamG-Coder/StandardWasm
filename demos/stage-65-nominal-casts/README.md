# Stage 65 · Managed reference casts

Run `npm run demo -- stage-65-nominal-casts`. Calling `Run(7)` returns `13`.
The C# fixtures recover a concrete control from an `object sender` with
`as` and explicit casts. Three unrelated classes have the same field
layout, including one from another DLL.

The CLR, Node and Edge suite checks identity, null, a same-layout
nonmatch, a boolean `is` expression, an event callback with a typed
sender, and both caught and uncaught typed `InvalidCastException`.
