# Stage 106 · Static field storage

Run with `npm run demo -- stage-106-static-field-storage`. The linked demo
returns 22. Run the CLR, Node and Edge comparison suite with `npm test`.

The compiler emits private Wasm globals for supported static fields. Since
[Stage 129](../stage-129-type-initializers/README.md), a declaring type's
initializer runs before the first access. Linked callers and methods in the defining
DLL share a slot identified by the assembly and FieldDef. The fixture covers
zero and null defaults, state across calls, narrow integer storage, scalar
widths, managed object identity and isolation of distinct declaring fields.

Field-level nullable annotations leave static storage and initializer semantics intact.
The fixture checks the annotation is present and compares the identity of repeated loads.
Each module instance executes one managed thread and owns that thread's
`ThreadStatic` slots. The fixture compares two CLR threads with fresh Wasm
instances and checks that a slot keeps its value across calls in one instance.
Managed threads sharing an assembly context need a separate storage model.

Other attributed statics, static fields of generic declaring types and static
addresses still require runtime support.
