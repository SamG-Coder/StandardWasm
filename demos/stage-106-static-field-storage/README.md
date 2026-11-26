# Stage 106 · Static field storage

Run with `npm run demo -- stage-106-static-field-storage`. The linked demo
returns 22. Run the CLR, Node and Edge comparison suite with `npm test`.

The compiler emits private Wasm globals for supported static fields whose
declaring type has no initializer. Linked callers and methods in the defining
DLL share a slot identified by the assembly and FieldDef. The fixture covers
zero and null defaults, state across calls, narrow integer storage, scalar
widths, managed object identity and isolation of distinct declaring fields.

Type initializers, attributed statics, static fields of generic declaring
types, value aggregates, and static addresses still require runtime support.
