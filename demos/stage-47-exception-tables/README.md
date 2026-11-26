# Stage 47 · CIL exception tables

Run `npm run demo -- stage-47-exception-tables`. The Wasm reader opens the .NET Standard DLL, locates `SmallCatch`, and reports one exception clause.

The reader handles compact and fat exception tables. It validates the file-backed bounds and encoded clause fields throughout a chain, then exposes the first table's clauses to match the .NET 10 CLR and `PEReader`. The tests compare ordinary .NET Standard catch, filter, and finally metadata and exercise malformed table copies and a short destination.

`npm test` runs the comparisons in Node and Chromium and checks the self-recompiled compiler's diagnostics. Valid managed handlers are still reported as unsupported. CIL control-flow validation and handler lowering are separate work.
