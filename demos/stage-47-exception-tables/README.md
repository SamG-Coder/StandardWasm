# Stage 47 · CIL exception tables

Run `npm run demo -- stage-47-exception-tables`. The Wasm reader opens the .NET Standard DLL, locates `SmallCatch`, and reports one exception clause.

The reader handles compact and fat exception tables, including chained sections. It checks file-backed PE bounds and the encoded try, handler, filter, and catch-token fields before publishing a clause. The tests compare ordinary .NET Standard catch, filter, and finally metadata against `PEReader` on the CLR and exercise malformed table copies and a short destination.

`npm test` runs the comparisons in Node and Chromium and checks the self-recompiled compiler's diagnostics. Valid managed handlers are still reported as unsupported. CIL control-flow validation and handler lowering are separate work.
