# 0050 · Dispatch class virtual calls by runtime type

The linker reserves a Wasm dispatcher when a class virtual receiver is not
proven exact. The declared CLI slot and the typed receiver bound are
separate. Original method bodies continue using the existing sealed and
allocation-proven paths when possible.

Reachable managed constructors identify possible concrete receivers. The
linker binds the original slot for every compatible allocation and compiles
the selected method bodies. Those bodies can allocate more concrete types
or introduce more virtual calls, so discovery continues until the method
queue and the dispatch cases reach a fixed point.

A dispatcher tests the nominal Wasm GC types from derived to base and
tail-calls the selected body. The caller checks null with the same typed
managed exception as a direct `callvirt`. The dispatcher's unreachable
fallback guards its closed-world invariant. Dynamic dispatch requires
entry signatures that cannot exchange managed classes, class arrays,
interfaces or delegates with the host. A returned mutable alias can also
let a foreign Wasm instance inject a compatible type. Managed interface calls, dynamic assembly loading and constrained
value calls have separate binding requirements. Unsupported explicit
method mappings retain their diagnostics when encountered.
