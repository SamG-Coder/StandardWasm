# Stage 50 · Nested and disjoint arithmetic catches

Run `npm run demo -- stage-50-nested-catches`. Calling
`HandlerEscapesToOuter(int.MaxValue, 0)` returns `-201`. The inner handler
catches divide-by-zero, then overflows while running inside an outer try.

The ordered exception table supports nested and disjoint arithmetic catches.
The demo fixtures cover inner precedence, outer fallback, a nested try
inside a handler, and an unhandled typed exception. They also verify that
the compiler reports malformed intersecting ranges and invalid clause order.

Run `npm test` to compare the same .NET Standard DLL on the CLR, Node and
Edge and to replay the cases with the generated compiler. Catch objects,
explicit managed throw, filters, fault and finally require further lowering.
