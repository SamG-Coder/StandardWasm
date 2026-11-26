# Stage 51 · Arithmetic exception rethrow

Run `npm run demo -- stage-51-arithmetic-rethrow`. Calling
`RetainOuter(int.MaxValue, 0)` returns `-101`. An inner handler catches an
overflow. When execution returns to the outer handler, C# `throw;` raises
its original divide-by-zero exception.

The compiler retains the Wasm exception reference for a handler and selects
the reference from the innermost enclosing catch at each rethrow. The CLR
fixtures cover both arithmetic tags, nested catches, propagation through
another call frame, and rethrow inside a try. Malformed fixtures cover
rethrow outside a catch, both with and without an EH table.

Run `npm test` for the CLR, Node and Edge comparisons and generated-compiler
replay. Managed exception objects and explicit throw operands, stack trace
APIs, filters, fault and finally require further lowering.
