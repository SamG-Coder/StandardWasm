# Stage 104 · Wide layout IDs

The linked library has a table with 311 reference fields and 313 managed layouts. The compiled entry allocates and writes the first and last field types, loads both fields, calls an inherited managed instance method, and checks a cast to the last class. The CLR oracle covers successful and invalid casts and arithmetic boundaries.

Run `npm run demo -- stage-104-wide-layout-ids`. The successful cast returns 24. The full suite also checks that undersized output capacity fails without writing a partial module.

Managed layout IDs occupy a separate range from scalar and signature keys. Evaluation-stack state retains those IDs until code emission. The emitter keeps the spill slots compact and writes Wasm heap indices with the encoding required by each operation.
