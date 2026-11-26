# Stage 54 · Nested finally and exceptional cleanup

Run `npm run demo -- stage-54-finally`. The generated `CatchAfterCleanup(0)`
returns `1234`. Two nested cleanup blocks update the trace before the
enclosing catch handles divide-by-zero.

The fixtures also exercise ordinary `leave`, multiple cleanup destinations,
a handler nested inside cleanup, preservation of a pending exception,
replacement exceptions, and malformed transfers. Run `npm test` for the
CLR, Node and Edge comparisons and generated-compiler replay.

The portable compiler unwinds exceptions that it represents with Wasm
exception tags. Native Wasm traps, managed filters, fault clauses, and
explicit managed throw operands require further work.
